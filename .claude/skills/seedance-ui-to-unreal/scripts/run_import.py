#!/usr/bin/env python
"""seedance-ui-to-unreal 的本地启动器（只用标准库）。

用法（Git Bash 里要加 MSYS_NO_PATHCONV=1，不然 /Game/... 会被改写）：
    python run_import.py --source <ui.zip|out目录> --project <UE 工程目录> --import-dir /Game/UI/X
                         [--wbp-name X] [--font /Game/Fonts/F] [--capture <png>]
stdout 只打印 ImportReport JSON；出错打印到 stderr。退出码：0 成功，1 导入错误，2 连接或环境错误。

流程：解压产物到 <工程>/Saved/SeedanceUiImport/<随机>/ → 本地检查（切图、记录可写）→ 确认编辑器开的是这个工程
→ 把 editor/seedance_ui 拼成一个脚本交给编辑器沙箱执行 → 写导入记录 → （可选）截图 → 清理解压目录。
"""

import argparse
import base64
import hashlib
import json
import os
import shutil
import sys
import urllib.error
import urllib.request
import uuid
import zipfile

SKILL_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EDITOR_DIR = os.path.join(SKILL_DIR, "editor")
BUNDLE_ORDER = ["errors", "layout", "naming", "geometry", "record", "planner", "tools", "apply", "main"]
SANDBOX = ("editor_toolset.toolsets.programmatic.ProgrammaticToolset", "execute_tool_script")
RECORD_ROOT = ("Content", "SeedanceKlab", "UI")


class LauncherError(Exception):
    """连接或环境问题（中文消息），退出码 2"""
    code = 2


class InputError(LauncherError):
    """产物、参数、记录文件的问题（中文消息），退出码 1"""
    code = 1


class ToolError(Exception):
    """编辑器里工具 / 脚本报错（消息是编辑器给的）"""


# ---------- 产物 ----------

def disk_path(p):
    """Git Bash 风格的 /d/foo 换成 D:/foo（MSYS_NO_PATHCONV=1 时 Git Bash 不会替我们换）"""
    if p and len(p) >= 3 and p[0] == "/" and p[1].isalpha() and p[2] == "/":
        return p[1].upper() + ":" + p[2:]
    if p and len(p) == 2 and p[0] == "/" and p[1].isalpha():
        return p[1].upper() + ":/"
    return p


def extract_source(source, project):
    """返回 (产物根目录, cleanup)。zip 解到 <工程>/Saved/SeedanceUiImport/<随机>/；目录直接用"""
    if os.path.isdir(source):
        return os.path.abspath(source), (lambda: None)
    if not (os.path.isfile(source) and source.lower().endswith(".zip")):
        raise InputError("产物路径不存在，或不是目录也不是 .zip：%s" % source)
    dest = os.path.join(os.path.abspath(project), "Saved", "SeedanceUiImport", uuid.uuid4().hex)
    os.makedirs(dest)
    cleanup = lambda: shutil.rmtree(dest, ignore_errors=True)
    try:
        with zipfile.ZipFile(source) as zf:
            for info in zf.infolist():
                name = info.filename
                if not info.flag_bits & 0x800:
                    try:
                        name = name.encode("cp437").decode("utf-8")  # 没打 UTF-8 标志的条目按 UTF-8 还原
                    except UnicodeError:
                        pass
                if name.endswith("/"):
                    continue
                target = os.path.abspath(os.path.join(dest, name))
                if os.path.isabs(name) or not target.startswith(dest + os.sep):
                    raise InputError("ui.zip 里有越出目录的条目：%s" % name)
                os.makedirs(os.path.dirname(target), exist_ok=True)
                with zf.open(info) as src, open(target, "wb") as out:
                    shutil.copyfileobj(src, out)
    except zipfile.BadZipFile as ex:
        cleanup()
        raise InputError("ui.zip 读不了：%s" % ex)
    except BaseException:
        cleanup()
        raise
    return dest, cleanup


def slice_images(layout):
    return sorted({e["image"] for e in layout.get("elements") or []
                   if e.get("image") and not e.get("missing") and e.get("type") != "text"})


def check_slices(layout, root):
    missing = [img for img in slice_images(layout) if not os.path.isfile(os.path.join(root, img))]
    if missing:
        raise InputError("产物里缺这些切图：%s" % "、".join(missing))


def slice_hashes(layout, root):
    out = {}
    for img in slice_images(layout):
        with open(os.path.join(root, img), "rb") as f:
            out[img] = hashlib.sha256(f.read()).hexdigest()
    return out


# ---------- 导入记录（本地读写） ----------

def check_import_dir(import_dir):
    if not (import_dir or "").replace("\\", "/").startswith("/Game/"):
        raise InputError("导入目录必须以 /Game/ 开头：%s（在 Git Bash 里调用要加 MSYS_NO_PATHCONV=1）" % import_dir)


def record_disk_path(project, wbp):
    rel = wbp[len("/Game/"):] + ".json"
    return os.path.join(os.path.abspath(project), *RECORD_ROOT, *rel.split("/"))


def check_record_writable(path):
    if os.path.exists(path) and not os.access(path, os.W_OK):
        raise InputError("导入记录是只读的（可能需要先在版本管理里 checkout）：%s。这时什么都还没写" % path)


def write_record(path, text):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".tmp"
        with open(tmp, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        os.replace(tmp, path)
    except OSError as ex:
        raise InputError("Widget Blueprint 已保存，但导入记录没写成：%s（%s）。不要直接重导（会把这次新建的控件再建一份）："
                            "先排除原因，再从版本库恢复这个 Widget Blueprint 后重导" % (path, ex))


# ---------- 拼脚本 ----------

def bundle_script(editor_dir, request):
    parts = ["# 由 run_import.py 拼接自 editor/seedance_ui/*.py，不要手改\n"]
    for mod in BUNDLE_ORDER:
        with open(os.path.join(editor_dir, "seedance_ui", mod + ".py"), encoding="utf-8") as f:
            lines = [ln for ln in f.read().splitlines() if not ln.startswith("from .")]
        parts.append("# ---- %s.py ----\n%s\n" % (mod, "\n".join(lines)))
    # 放在最后：main.py 里有 REQUEST = None
    parts.append("REQUEST = json.loads(%r)\n" % json.dumps(request, ensure_ascii=False))
    return "\n".join(parts)


# ---------- MCP ----------

def mcp_endpoint(project):
    path = os.path.join(project, ".mcp.json")
    try:
        with open(path, encoding="utf-8") as f:
            servers = json.load(f).get("mcpServers") or {}
    except (OSError, ValueError):
        raise LauncherError("读不了 %s。在编辑器控制台运行 ModelContextProtocol.GenerateClientConfig ClaudeCode 生成它" % path)
    for cfg in servers.values():
        if isinstance(cfg, dict) and cfg.get("url"):
            return cfg["url"]
    raise LauncherError("%s 里没有 MCP 服务器地址" % path)


class McpClient:
    def __init__(self, url, timeout=900):
        self.url, self.timeout, self.session = url, timeout, None

    def post(self, body):
        req = urllib.request.Request(self.url, data=json.dumps(body).encode("utf-8"), method="POST")
        req.add_header("Content-Type", "application/json")
        req.add_header("Accept", "application/json, text/event-stream")
        if self.session:
            req.add_header("Mcp-Session-Id", self.session)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                return dict(r.headers), r.read().decode("utf-8")
        except (urllib.error.URLError, OSError) as ex:
            raise LauncherError("连不上编辑器的 MCP 服务器 %s（%s）。编辑器开着吗？没起来就在控制台敲 "
                                "ModelContextProtocol.StartServer" % (self.url, ex))

    def connect(self):
        if self.session:
            return
        headers, _ = self.post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
            "protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "seedance-ui-to-unreal", "version": "1"}}})
        self.session = next((v for k, v in headers.items() if k.lower() == "mcp-session-id"), None)
        self.post({"jsonrpc": "2.0", "method": "notifications/initialized"})

    def call_tool(self, toolset, tool, args):
        """返回工具的 returnValue（原样）；工具报错抛 ToolError"""
        self.connect()
        _, raw = self.post({"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {
            "name": "call_tool", "arguments": {"toolset_name": toolset, "tool_name": tool, "arguments": args}}})
        res = json.loads(raw).get("result") or {}
        text = "".join(c.get("text", "") for c in res.get("content", []) if c.get("type") == "text")
        if res.get("isError"):
            raise ToolError(text)
        return json.loads(text).get("returnValue") if text else None


def check_project(client, project):
    """在 <工程>/Saved/SeedanceUiImport/ 放一个随机标记让编辑器读回来：读得到 = 编辑器开的就是这个工程。用完删掉"""
    token = uuid.uuid4().hex
    folder = os.path.join(os.path.abspath(project), "Saved", "SeedanceUiImport")
    os.makedirs(folder, exist_ok=True)
    marker = os.path.join(folder, "marker-%s.txt" % token)
    with open(marker, "w") as f:
        f.write(token)
    script = ("import json\ndef run():\n    return {'t': execute_tool('editor_toolset.toolsets.asset.AssetTools.read_file', "
              "json.dumps({'file_path': %r}))['returnValue']}\n" % marker.replace("\\", "/"))
    try:
        got = json.loads(client.call_tool(SANDBOX[0], SANDBOX[1], {"script": script}))["t"]
    except ToolError as ex:
        raise LauncherError("编辑器打开的不是这个工程（它读不到 %s）。确认 .mcp.json 的端口属于目标工程的编辑器。\n%s"
                            % (marker, str(ex)[:300]))
    finally:
        os.remove(marker)
    if got.strip() != token:
        raise LauncherError("编辑器打开的不是这个工程（标记文件内容对不上）")


def capture(client, wbp, out_png):
    """打开 WBP 编辑器窗口截图（整个窗口），用完关掉"""
    editor, slate = "EditorToolset.EditorAppToolset", "SlateInspectorToolset.SlateInspectorToolset"
    client.call_tool(editor, "OpenEditorForAsset", {"assetPath": wbp})
    windows = client.call_tool(slate, "Windows", {"action": "list"})
    windows = json.loads(windows) if isinstance(windows, str) else windows
    name = wbp.rsplit("/", 1)[-1]
    idx = next((w["index"] for w in windows if w.get("title") == name), None)
    if idx is None:
        raise LauncherError("截图：找不到 %s 的编辑器窗口" % name)
    client.call_tool(slate, "Windows", {"action": "select", "index": idx})
    img = client.call_tool(slate, "Screenshot", {"ref": ""})
    img = json.loads(img) if isinstance(img, str) else img
    os.makedirs(os.path.dirname(os.path.abspath(out_png)), exist_ok=True)
    with open(out_png, "wb") as f:
        f.write(base64.b64decode(img["data"]))
    client.call_tool(slate, "Windows", {"action": "close", "index": idx})
    return os.path.abspath(out_png)


# ---------- 入口 ----------

def wbp_of(import_dir, wbp_name):
    sys.path.insert(0, EDITOR_DIR)
    from seedance_ui.errors import UiImportError
    from seedance_ui.naming import wbp_path
    try:
        return wbp_path(import_dir, wbp_name or None)
    except UiImportError as ex:
        raise InputError(str(ex))


def import_ui(args):
    check_import_dir(args.import_dir)
    wbp = wbp_of(args.import_dir, args.wbp_name)
    if not os.path.isdir(os.path.join(args.project, "Content")):
        raise InputError("不是 UE 工程目录（没有 Content/）：%s" % args.project)
    client = McpClient(mcp_endpoint(args.project))
    root, cleanup = extract_source(args.source, args.project)
    try:
        layout_path = os.path.join(root, "layout.json")
        if not os.path.isfile(layout_path):
            raise InputError("产物里没有 layout.json：%s" % args.source)
        with open(layout_path, encoding="utf-8") as f:
            layout_text = f.read()
        try:
            layout = json.loads(layout_text)
        except ValueError as ex:
            raise InputError("layout.json 不是合法的 JSON：%s" % ex)
        check_slices(layout, root)
        rec_path = record_disk_path(args.project, wbp)
        check_record_writable(rec_path)
        record_text = None
        if os.path.exists(rec_path):
            with open(rec_path, encoding="utf-8") as f:
                record_text = f.read()
        check_project(client, args.project)
        request = {"importDir": args.import_dir, "wbpName": args.wbp_name or "", "font": args.font or "",
                   "layoutJson": layout_text, "sliceRoot": root.replace("\\", "/"),
                   "hashes": slice_hashes(layout, root), "recordText": record_text}
        result = json.loads(client.call_tool(SANDBOX[0], SANDBOX[1], {"script": bundle_script(EDITOR_DIR, request)}))
        write_record(rec_path, result["recordText"])
        report = result["report"]
        if args.capture:
            report["capture"] = capture(client, wbp, args.capture)
        return report
    finally:
        cleanup()


def main(argv=None):
    ap = argparse.ArgumentParser(description="Seedance ui-maker 产物 → UMG Widget Blueprint")
    ap.add_argument("--source", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--import-dir", required=True)
    ap.add_argument("--wbp-name", default="")
    ap.add_argument("--font", default="")
    ap.add_argument("--capture", default="")
    args = ap.parse_args(argv)
    args.source, args.project, args.capture = disk_path(args.source), disk_path(args.project), disk_path(args.capture)
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    try:
        report = import_ui(args)
    except ToolError as ex:
        print(str(ex), file=sys.stderr)
        return 1
    except LauncherError as ex:
        print(str(ex), file=sys.stderr)
        return ex.code
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
