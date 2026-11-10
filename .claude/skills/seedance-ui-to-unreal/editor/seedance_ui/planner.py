# 生成操作计划：首次导入建全部控件；重导按记录认回控件，只更新贴图 / 位置 / 尺寸（规则见设计第 2 节）。
# 纯逻辑，不碰 MCP；apply.py 照 ops 执行。
#
# ops（按顺序执行）：
#   {"op": "ensure_root", "width", "height"}
#   {"op": "add_container", "name", "parent", "offsets": (l, t, w, h)}      追加到 parent 的子节点末尾
#   {"op": "add_view", "name", "container", "cls", "texture"|None, "element"}  追加到容器（此时容器还没有子节点 → 第一个）
#   {"op": "set_offsets", "name", "offsets"}        容器的位置与尺寸
#   {"op": "set_size", "name", "w", "h"}            只改尺寸（被程序挪走的容器）
#   {"op": "set_texture", "view", "cls", "texture", "w", "h"}
#   {"op": "move", "name", "parent", "after"|None}  挪到 parent 下、排在容器 after 之后（None = 显示控件之后的第一个）
#   {"op": "set_visibility", "name", "value"}

from .naming import container_name, texture_name, view_class, view_name
from .geometry import slot_offsets
from .record import ROOT_PARENT, element_entry, new_record

CONTAINER_VISIBILITY = "SelfHitTestInvisible"


def label(el):
    return "%s %s" % (el["id"], el["name"])


def empty_report(wbp):
    return {"widgetBlueprint": wbp, "created": [], "updated": [], "rebuilt": [], "deactivated": [], "missing": [],
            "moved": [], "typeChanged": [], "orphanTextures": [], "warnings": []}


def creation_order(doc):
    """父先于子；同一父下按数组顺序（深度优先、按数组顺序展开）"""
    children = {}
    for el in doc["elements"]:
        children.setdefault(el["parent"], []).append(el)
    out = []

    def walk(pid):
        for el in children.get(pid, []):
            out.append(el)
            walk(el["id"])
    walk(None)
    return out


def texture_list(doc, hashes):
    tex = {}
    for el in doc["elements"]:
        if el["missing"] or el["type"] == "text" or not el["image"]:
            continue
        name = texture_name(doc, el)
        master = doc["by_id"][el["same_as"]] if el["same_as"] else el
        entry = tex.setdefault(name, {"name": name, "source_image": master["image"] or el["image"],
                                      "hash": None, "elements": []})
        entry["elements"].append(el["id"])
    for entry in tex.values():
        entry["hash"] = hashes.get(entry["source_image"])
    return [tex[k] for k in sorted(tex)]


def element_texture(doc, el):
    if el["missing"] or el["type"] == "text" or not el["image"]:
        return None
    return texture_name(doc, el)


def build_ops(doc, el, parent_name, out):
    """新建一个元素：容器 + 显示控件"""
    parent_el = doc["by_id"][el["parent"]] if el["parent"] else None
    out.append({"op": "add_container", "name": container_name(el), "parent": parent_name,
                "offsets": slot_offsets(el, parent_el)})
    vname = view_name(el)
    if vname:
        out.append({"op": "add_view", "name": vname, "container": container_name(el), "cls": view_class(el["type"]),
                    "texture": element_texture(doc, el), "element": el})


def plan(doc, wbp, record, tree, hashes):
    if record is None:
        return plan_create(doc, wbp, hashes)
    return plan_update(doc, wbp, record, tree, hashes)


def plan_create(doc, wbp, hashes):
    report = empty_report(wbp)
    ops = [{"op": "ensure_root", "width": doc["width"], "height": doc["height"]}]
    rec = new_record(wbp, doc)
    design = rec["rootWidgets"]["design"]
    for el in creation_order(doc):
        parent_name = container_name(doc["by_id"][el["parent"]]) if el["parent"] else design
        build_ops(doc, el, parent_name, ops)
        report["created"].append(label(el))
        if el["missing"]:
            report["missing"].append(label(el))
    rec["elements"] = [element_entry(el) for el in doc["elements"]]
    textures = texture_list(doc, hashes)
    rec["textures"] = {t["name"]: t["hash"] for t in textures}
    return {"ops": ops, "report": report, "record": rec, "textures": textures}


def plan_update(doc, wbp, record, tree, hashes):
    raise NotImplementedError
