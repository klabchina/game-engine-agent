import ast
import importlib.util
import io
import json
import os
import re
import shutil
import stat
import tempfile
import threading
import unittest
import zipfile
from http.server import BaseHTTPRequestHandler, HTTPServer

from fake_editor import FakeEditor
from fixtures import layout_dict, zip_path

HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".claude", "skills", "seedance-ui-to-unreal"))
spec = importlib.util.spec_from_file_location("run_import", os.path.join(SKILL, "scripts", "run_import.py"))
run_import = importlib.util.module_from_spec(spec)
spec.loader.exec_module(run_import)
EDITOR_DIR = os.path.join(SKILL, "editor")


def make_project(tmp):
    proj = os.path.join(tmp, "Proj")
    os.makedirs(os.path.join(proj, "Content"))
    with open(os.path.join(proj, "Proj.uproject"), "w") as f:
        f.write("{}")
    with open(os.path.join(proj, ".mcp.json"), "w") as f:
        json.dump({"mcpServers": {"unreal-mcp": {"type": "http", "url": "http://127.0.0.1:1/mcp"}}}, f)
    return proj


class LauncherTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, self.tmp, True)
        self.proj = make_project(self.tmp)

    def test_extract_zip_utf8_names(self):
        z = os.path.join(self.tmp, "u.zip")
        with zipfile.ZipFile(z, "w") as zf:
            info = zipfile.ZipInfo("slices/格子_1.png")
            zf.writestr(info, b"png")  # ZipFile 写非 ASCII 名会自动置 UTF-8 标志；下面再造一个不带标志的
        raw = bytearray(open(z, "rb").read())
        # 清掉通用标志位的第 11 位（UTF-8），模拟 macOS / JSZip 打的包
        for sig in (b"PK\x03\x04", b"PK\x01\x02"):
            i = raw.find(sig)
            off = i + (6 if sig == b"PK\x03\x04" else 8)
            flags = int.from_bytes(raw[off:off + 2], "little") & ~0x800
            raw[off:off + 2] = flags.to_bytes(2, "little")
        open(z, "wb").write(bytes(raw))
        root, cleanup = run_import.extract_source(z, self.proj)
        self.assertTrue(os.path.isfile(os.path.join(root, "slices", "格子_1.png")))
        self.assertTrue(root.replace("\\", "/").startswith(self.proj.replace("\\", "/") + "/Saved/SeedanceUiImport/"))
        cleanup()
        self.assertFalse(os.path.exists(root))

    def test_extract_rejects_traversal(self):
        z = os.path.join(self.tmp, "evil.zip")
        with zipfile.ZipFile(z, "w") as zf:
            zf.writestr("../evil.png", b"x")
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.extract_source(z, self.proj)
        self.assertIn("evil.png", str(cm.exception))
        self.assertFalse(os.path.exists(os.path.join(self.proj, "Saved", "evil.png")))

    def test_extract_real_fixture_and_check_slices(self):
        root, cleanup = run_import.extract_source(zip_path("ui-status"), self.proj)
        self.addCleanup(cleanup)
        layout = layout_dict("ui-status")
        run_import.check_slices(layout, root)
        hashes = run_import.slice_hashes(layout, root)
        self.assertEqual(len(hashes["slices/面板.png"]), 64)
        os.remove(os.path.join(root, "slices", "升级.png"))
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.check_slices(layout, root)
        self.assertIn("slices/升级.png", str(cm.exception))

    def test_bundle_has_no_duplicate_top_level_names(self):
        seen = {}
        for mod in run_import.BUNDLE_ORDER:
            tree = ast.parse(open(os.path.join(EDITOR_DIR, "seedance_ui", mod + ".py"), encoding="utf-8").read())
            for node in tree.body:
                names = []
                if isinstance(node, (ast.FunctionDef, ast.ClassDef)):
                    names = [node.name]
                elif isinstance(node, ast.Assign):
                    names = [t.id for t in node.targets if isinstance(t, ast.Name)]
                for n in names:
                    self.assertNotIn(n, seen, "%s 同时出现在 %s 和 %s" % (n, seen.get(n), mod))
                    seen[n] = mod

    def test_bundle_imports_only_allowed_modules(self):
        script = run_import.bundle_script(EDITOR_DIR, {"x": 1})
        for line in script.splitlines():
            m = re.match(r"\s*(?:from\s+(\S+)\s+import|import\s+(.+))", line)
            if m:
                mods = [m.group(1)] if m.group(1) else [x.strip().split(" ")[0] for x in m.group(2).split(",")]
                for mod in mods:
                    self.assertIn(mod, ("json", "re", "math"), line)

    def test_bundle_compiles_and_runs(self):
        layout = layout_dict("ui-status")
        req = {"importDir": "/Game/Tests/X", "wbpName": "", "font": "", "layoutJson": json.dumps(layout),
               "sliceRoot": "C:/x", "hashes": {}, "recordText": None}
        script = run_import.bundle_script(EDITOR_DIR, req)
        code = compile(script, "<bundle>", "exec")
        fake = FakeEditor()
        ns = {"execute_tool": fake.execute_tool, "__name__": "bundle"}
        exec(code, ns)
        out = ns["run"]()
        self.assertEqual(len(out["report"]["created"]), 65)
        self.assertIn("/Game/Tests/X/WBP_X", fake.wbps)

    def test_record_paths(self):
        p = run_import.record_disk_path(self.proj, "/Game/UI/X/WBP_X")
        self.assertEqual(p.replace("\\", "/"), self.proj.replace("\\", "/") + "/Content/SeedanceKlab/UI/UI/X/WBP_X.json")

    def test_write_record_atomic_and_read_only(self):
        p = run_import.record_disk_path(self.proj, "/Game/UI/X/WBP_X")
        run_import.write_record(p, "a")
        self.assertEqual(open(p, encoding="utf-8").read(), "a")
        os.chmod(p, stat.S_IREAD)
        self.addCleanup(os.chmod, p, stat.S_IWRITE | stat.S_IREAD)
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.check_record_writable(p)
        self.assertIn("只读", str(cm.exception))
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.write_record(p, "b")
        self.assertIn("不要直接重导", str(cm.exception))

    def test_mcp_endpoint(self):
        self.assertEqual(run_import.mcp_endpoint(self.proj), "http://127.0.0.1:1/mcp")
        os.remove(os.path.join(self.proj, ".mcp.json"))
        with self.assertRaises(run_import.LauncherError):
            run_import.mcp_endpoint(self.proj)

    def test_rejects_mangled_import_dir(self):
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.check_import_dir("C:/Program Files/Git/Game/UI/X")
        self.assertIn("MSYS_NO_PATHCONV", str(cm.exception))
        run_import.check_import_dir("/Game/UI/X")


class StubMcp(BaseHTTPRequestHandler):
    calls = []

    def log_message(self, *a):
        pass

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        StubMcp.calls.append((body.get("method"), self.headers.get("Mcp-Session-Id")))
        if body["method"] == "initialize":
            self.reply({"jsonrpc": "2.0", "id": body["id"], "result": {}}, {"Mcp-Session-Id": "s1"})
        elif body["method"] == "notifications/initialized":
            self.send_response(202)
            self.end_headers()
        else:
            args = body["params"]["arguments"]
            if args["tool_name"] == "boom":
                result = {"content": [{"type": "text", "text": "line 3: UiImportError: 坏了"}], "isError": True}
            else:
                result = {"content": [{"type": "text", "text": json.dumps({"returnValue": json.dumps({"ok": 1})})}]}
            self.reply({"jsonrpc": "2.0", "id": body["id"], "result": result})

    def reply(self, obj, headers=None):
        data = json.dumps(obj).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        for k, v in (headers or {}).items():
            self.send_header(k, v)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


class McpClientTests(unittest.TestCase):
    def setUp(self):
        self.srv = HTTPServer(("127.0.0.1", 0), StubMcp)
        threading.Thread(target=self.srv.serve_forever, daemon=True).start()
        self.addCleanup(self.srv.shutdown)
        self.url = "http://127.0.0.1:%d/mcp" % self.srv.server_port
        StubMcp.calls = []

    def test_mcp_client_against_stub(self):
        c = run_import.McpClient(self.url)
        self.assertEqual(c.call_tool("ts", "ok", {}), json.dumps({"ok": 1}))
        self.assertEqual(StubMcp.calls[0], ("initialize", None))
        self.assertEqual(StubMcp.calls[1], ("notifications/initialized", "s1"))
        self.assertEqual(StubMcp.calls[2], ("tools/call", "s1"))

    def test_mcp_client_error(self):
        c = run_import.McpClient(self.url)
        with self.assertRaises(run_import.ToolError) as cm:
            c.call_tool("ts", "boom", {})
        self.assertIn("坏了", str(cm.exception))

    def test_connection_refused(self):
        with self.assertRaises(run_import.LauncherError) as cm:
            run_import.McpClient("http://127.0.0.1:1/mcp").call_tool("ts", "ok", {})
        self.assertIn("127.0.0.1:1", str(cm.exception))


if __name__ == "__main__":
    unittest.main()
