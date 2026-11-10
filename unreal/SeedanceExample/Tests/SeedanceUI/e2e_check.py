"""seedance-ui-to-unreal 端到端检查（SeedanceExample 编辑器必须开着）。由 run-e2e.sh 调用。

1. 两个产物分别导入 /Game/Tests/SeedanceUI/Popup、Status，断言报告。
2. 经 MCP 读回 Status 的槽 / 文字属性断言坐标与字号。
3. 改名一个显示控件、手加一个控件、打开 WBP 编辑器，再重导 Status：都保留、无重复、无 rebuilt。
4. 截图到 <输出目录>/status.png、popup.png，并解出两个 preview.png，供人眼对照。
5. 清理 /Game/Tests/SeedanceUI 与对应导入记录。
"""

import argparse
import importlib.util
import json
import os
import shutil
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
SKILL = os.path.normpath(os.path.join(PROJECT, "..", "..", ".claude", "skills", "seedance-ui-to-unreal"))
spec = importlib.util.spec_from_file_location("run_import", os.path.join(SKILL, "scripts", "run_import.py"))
ri = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ri)

ROOT = "/Game/Tests/SeedanceUI"
STATUS = ROOT + "/Status/WBP_Status"
SANDBOX = ri.SANDBOX
failures = []


def check(cond, what):
    print(("ok   " if cond else "FAIL ") + what)
    if not cond:
        failures.append(what)


def sandbox(client, body):
    script = "import json\n" + body
    return json.loads(client.call_tool(SANDBOX[0], SANDBOX[1], {"script": script}))


def cleanup(client):
    sandbox(client, "def run():\n    e = execute_tool('editor_toolset.toolsets.asset.AssetTools.exists', json.dumps({'path': %r}))['returnValue']\n"
                    "    if e:\n        execute_tool('editor_toolset.toolsets.asset.AssetTools.delete', json.dumps({'path': %r}))\n"
                    "    return {}\n" % (ROOT, ROOT))
    shutil.rmtree(os.path.join(PROJECT, "Content", "SeedanceKlab", "UI", "Tests"), ignore_errors=True)


def do_import(name, sub, capture=""):
    args = argparse.Namespace(source=os.path.join(PROJECT, "TestFixtures", name + ".zip"), project=PROJECT,
                              import_dir="%s/%s" % (ROOT, sub), wbp_name="", font="", capture=capture)
    return ri.import_ui(args)


def widget_props(client, wbp, name, slot_props, props):
    obj = "%s.%s" % (wbp, wbp.rsplit("/", 1)[-1])
    body = ("def run():\n"
            "    w = {'refPath': %r}\n"
            "    slot = json.loads(execute_tool('editor_toolset.toolsets.object.ObjectTools.get_properties', json.dumps({'instance': w, 'properties': ['slot']}))['returnValue'])['slot']\n"
            "    out = {}\n"
            "    if %r:\n"
            "        out['slot'] = json.loads(execute_tool('editor_toolset.toolsets.object.ObjectTools.get_properties', json.dumps({'instance': slot, 'properties': %r}))['returnValue'])\n"
            "    if %r:\n"
            "        out['w'] = json.loads(execute_tool('editor_toolset.toolsets.object.ObjectTools.get_properties', json.dumps({'instance': w, 'properties': %r}))['returnValue'])\n"
            "    return out\n") % ("%s:WidgetTree.%s" % (obj, name), slot_props, slot_props, props, props)
    return sandbox(client, body)


def tree_names(client, wbp):
    obj = "%s.%s" % (wbp, wbp.rsplit("/", 1)[-1])
    body = ("def run():\n    r = execute_tool('UMGToolSet.UMGToolSet.GetWidgets', json.dumps({'widgetBlueprint': {'refPath': %r}}))['returnValue']\n"
            "    return {'names': [w['widgetName'] for w in r['widgets']]}\n") % obj
    return sandbox(client, body)["names"]


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(PROJECT, "Saved", "SeedanceE2E")
    os.makedirs(out_dir, exist_ok=True)
    client = ri.McpClient(ri.mcp_endpoint(PROJECT))
    cleanup(client)

    layouts = {}
    for name in ("ui-popup", "ui-status"):
        with zipfile.ZipFile(os.path.join(PROJECT, "TestFixtures", name + ".zip")) as z:
            layouts[name] = json.loads(z.read("layout.json").decode("utf-8"))
            with open(os.path.join(out_dir, name + "-preview.png"), "wb") as f:
                f.write(z.read("preview.png"))

    rep = do_import("ui-popup", "Popup", os.path.join(out_dir, "popup.png"))
    check(len(rep["created"]) == len(layouts["ui-popup"]["elements"]), "popup 首导 created = %d" % len(rep["created"]))
    check(rep["warnings"] == [], "popup 首导无 warning：%s" % rep["warnings"])

    rep = do_import("ui-status", "Status")
    check(len(rep["created"]) == 65, "status 首导 created = %d" % len(rep["created"]))
    check(not os.path.exists(os.path.join(PROJECT, "Saved", "SeedanceUiImport")) or
          not os.listdir(os.path.join(PROJECT, "Saved", "SeedanceUiImport")), "解压目录与标记文件已清理")
    rec_path = ri.record_disk_path(PROJECT, STATUS)
    check(os.path.isfile(rec_path), "导入记录已写：%s" % rec_path)

    p = widget_props(client, STATUS, "button_e62", ["layoutData"], [])
    off = p["slot"]["layoutData"]["offsets"]
    check((off["left"], off["top"], off["right"], off["bottom"]) == (1367 - 237, 1157 - 127, 286, 105), "button_e62 偏移 %s" % off)
    e3 = next(e for e in layouts["ui-status"]["elements"] if e["id"] == "e3")
    p = widget_props(client, STATUS, "text_e3_Text", [], ["text", "font"])
    check(p["w"]["text"] == "角色属性", "text_e3_Text 文字 %s" % p["w"]["text"])
    check(abs(p["w"]["font"]["size"] - e3["text"]["fontSize"] * 0.75) < 0.01, "text_e3_Text 字号 %s" % p["w"]["font"]["size"])
    # 真实编辑器上跑 tools.widget_texture_refs（贴图替换前查本 WBP 内引用要靠它）
    src = []
    for mod in ("errors", "tools"):
        with open(os.path.join(SKILL, "editor", "seedance_ui", mod + ".py"), encoding="utf-8") as f:
            src.append("\n".join(l for l in f.read().splitlines() if not l.startswith("from .")))
    src.append("def run():\n    return {'refs': sorted(widget_texture_refs(%r, 'button_e62_Button', '/Script/UMG.Button'))}\n" % STATUS)
    refs = json.loads(client.call_tool(SANDBOX[0], SANDBOX[1], {"script": "\n".join(src)}))["refs"]
    check("/Game/Tests/SeedanceUI/Status/Textures/T_button_e62.T_button_e62" in refs, "widget_texture_refs 读到按钮贴图 %s" % refs)
    # 同一导入目录再导一套别的 UI 要被拦下
    try:
        args = argparse.Namespace(source=os.path.join(PROJECT, "TestFixtures", "ui-popup.zip"), project=PROJECT,
                                  import_dir=ROOT + "/Status", wbp_name="Other", font="", capture="")
        ri.import_ui(args)
        check(False, "同目录第二套 UI 应被拦下")
    except ri.ToolError as ex:
        check("只放一套 UI" in str(ex), "同目录第二套 UI 被拦下")

    p = widget_props(client, STATUS, "SizeBox", [], ["widthOverride", "heightOverride"])
    check((p["w"]["widthOverride"], p["w"]["heightOverride"]) == (2560, 1440), "SizeBox %s" % p["w"])

    # 程序改动：改名显示控件、手加控件；打开 WBP 编辑器后重导
    obj = "%s.%s" % (STATUS, "WBP_Status")
    sandbox(client, ("def run():\n"
                     "    wbp = {'refPath': %r}\n"
                     "    execute_tool('UMGToolSet.UMGToolSet.RenameWidget', json.dumps({'widgetBlueprint': wbp, 'widget': {'refPath': %r}, 'newDisplayName': 'UpgradeButton'}))\n"
                     "    execute_tool('UMGToolSet.UMGToolSet.AddWidget', json.dumps({'widgetBlueprint': wbp, 'widgetClass': {'refPath': '/Script/UMG.Button'}, 'widgetDisplayName': 'MyButton', 'parentWidget': {'refPath': %r}}))\n"
                     "    execute_tool('UMGToolSet.UMGToolSet.CompileWidgetBlueprint', json.dumps({'widgetBlueprint': wbp}))\n"
                     "    execute_tool('editor_toolset.toolsets.asset.AssetTools.save_assets', json.dumps({'asset_paths': [%r]}))\n"
                     "    return {}\n") % (obj, obj + ":WidgetTree.button_e62_Button", obj + ":WidgetTree.panel_e1", STATUS))
    before = tree_names(client, STATUS)
    check("UpgradeButton" in before and "MyButton" in before, "改名与手加已生效")
    client.call_tool("EditorToolset.EditorAppToolset", "OpenEditorForAsset", {"assetPath": STATUS})
    rep = do_import("ui-status", "Status", os.path.join(out_dir, "status.png"))
    after = tree_names(client, STATUS)
    check("UpgradeButton" in after and "MyButton" in after, "重导后改名控件与手加控件都在")
    check("button_e62_Button" not in after, "没有重建出 button_e62_Button")
    check(len(after) == len(before), "控件总数不变 %d → %d" % (len(before), len(after)))
    check(rep["rebuilt"] == [] and len(rep["updated"]) == 65, "重导 rebuilt=%s updated=%d" % (rep["rebuilt"], len(rep["updated"])))
    with open(rec_path, encoding="utf-8") as f:
        rec = json.load(f)
    check(next(e for e in rec["elements"] if e["elementId"] == "e62")["view"] == "UpgradeButton", "记录写回新名字")

    cleanup(client)
    check(not os.path.exists(rec_path), "测试记录已清理")
    print("\n截图：%s" % out_dir)
    if failures:
        print("\n%d 项失败" % len(failures))
        return 1
    print("\n全部通过")
    return 0


if __name__ == "__main__":
    for s in (sys.stdout, sys.stderr):
        s.reconfigure(encoding="utf-8")
    sys.exit(main())
