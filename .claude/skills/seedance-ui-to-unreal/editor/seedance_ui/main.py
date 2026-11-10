# 沙箱入口：启动器在拼接脚本开头放 REQUEST，再调 run()。返回 {"report": ImportReport, "recordText": 新的导入记录}。
# REQUEST = {importDir, wbpName, font, layoutJson, sliceRoot, hashes: {image: sha256}, recordText: str|None}

from .layout import parse_layout
from .naming import texture_folder, wbp_path
from .record import dump_record, load_record
from .planner import plan
from .apply import Executor, TreeMirror, check_textures_editable, compile_check, preflight, textures_to_replace, update_textures
from .tools import asset_exists, compile_wbp, create_wbp, font_typefaces, list_assets, read_tree, save_assets

REQUEST = None


def run():
    req = REQUEST
    import_dir = (req.get("importDir") or "").replace("\\", "/").rstrip("/")
    wbp = wbp_path(import_dir, req.get("wbpName") or None)
    doc = parse_layout(req["layoutJson"])
    record = load_record(req["recordText"], wbp) if req.get("recordText") else None
    font = req.get("font") or ""
    exists = asset_exists(wbp)
    preflight(wbp, exists, record, font)
    tree = read_tree(wbp) if exists else {}
    p = plan(doc, wbp, record, tree if exists else None, req.get("hashes") or {})
    check_textures_editable(textures_to_replace(import_dir, p["textures"]))
    faces = font_typefaces(font or None) if any(e["text"] and e["text"]["bold"] for e in doc["elements"]) else []
    if exists:
        compile_check(wbp)

    # ---- 以下开始写 ----
    report = p["report"]
    rec = p["record"]
    own_views = set(o["view"] for o in p["ops"] if o["op"] == "set_texture") | \
        set(o["name"] for o in p["ops"] if o["op"] == "add_view")
    update_textures(import_dir, wbp, p["textures"], rec, report, req["sliceRoot"], tree, own_views)
    if not exists:
        folder, name = wbp.rsplit("/", 1)
        create_wbp(folder, name)
    views = set(e["view"] for e in rec["elements"] if e.get("view"))
    executor = Executor(wbp, import_dir, font, faces, report, TreeMirror(tree, views))
    executor.run(p["ops"], rec["rootWidgets"], doc["width"], doc["height"])
    compile_wbp(wbp)
    save_assets([wbp])

    used = set("%s/%s" % (texture_folder(import_dir), t["name"]) for t in p["textures"])
    report["orphanTextures"] = sorted(a for a in list_assets(texture_folder(import_dir)) if a not in used)
    return {"report": report, "recordText": dump_record(rec)}

