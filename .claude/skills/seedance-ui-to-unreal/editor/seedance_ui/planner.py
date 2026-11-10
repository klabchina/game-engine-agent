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

from .errors import UiImportError
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


def resolve_entry(entry, tree):
    """按控件名认回一个已导入的元素：先容器名，再显示控件名的父。返回 (容器名, 显示控件名|None)，认不出返回 None"""
    view = entry.get("view")
    container = entry["container"]
    if container in tree:
        if view and tree.get(view, {}).get("parent") == container:
            return (container, view)
        if view:
            # 显示控件改了名：取容器的第一个子节点，类型要对得上
            want = view_class(entry["type"])
            first = [n for n, w in tree.items() if w["parent"] == container and w["index"] == 0 and w["class"] == want]
            return (container, first[0] if first else None)
        return (container, None)
    if view and view in tree and tree[view]["parent"]:
        return (tree[view]["parent"], view)
    return None


def plan_update(doc, wbp, record, tree, hashes):
    roots = record["rootWidgets"]
    design = roots["design"]
    if design not in tree:
        raise UiImportError("Widget Blueprint 的根结构被改动过，找不到 %s 控件，没法重导。从版本库恢复，或删掉它和导入记录后重新导入" % design)
    report = empty_report(wbp)
    ops = []
    entries = {}
    for e in record["elements"]:
        entries[e["elementId"]] = dict(e)
    current = {}   # elementId → (容器名, 显示控件名|None)：认回的旧节点和本次新建的节点
    for eid, e in entries.items():
        found = resolve_entry(e, tree)
        if found:
            current[eid] = found
            e["container"], e["view"] = found[0], found[1]

    def container_of(eid):
        return current[eid][0] if eid in current else None

    def layout_parent_name(el):
        return container_of(el["parent"]) if el["parent"] else design

    def recorded_parent_name(entry):
        lp = entry.get("layoutParentId")
        if lp == ROOT_PARENT:
            return design
        return container_of(lp)

    def previous_sibling(el):
        """数组里排在 el 前面、同一个 layout 父、已在那个父下的最近元素的容器名"""
        target = layout_parent_name(el)
        idx = doc["elements"].index(el)
        for prev in reversed(doc["elements"][:idx]):
            if prev["parent"] != el["parent"] or prev["id"] not in current:
                continue
            name = current[prev["id"]][0]
            if tree_parent.get(name) == target:
                return name
        return None

    tree_parent = {n: w["parent"] for n, w in tree.items()}
    new_entries = {}
    for el in creation_order(doc):
        eid = el["id"]
        entry = entries.get(eid)
        if entry is None or eid not in current:
            parent_name = layout_parent_name(el)
            before = len(ops)
            build_ops(doc, el, parent_name, ops)
            ops[before]["after"] = previous_sibling(el)
            current[eid] = (container_name(el), view_name(el))
            tree_parent[container_name(el)] = parent_name
            new_entries[eid] = element_entry(el)
            report["rebuilt" if entry is not None else "created"].append(label(el))
            if el["missing"]:
                report["missing"].append(label(el))
            continue

        cname, vname = current[eid]
        if entry["type"] != el["type"]:
            report["typeChanged"].append(label(el))
            report["warnings"].append("%s：类型从 %s 变成了 %s，控件没换，需要的话手动调整" % (label(el), entry["type"], el["type"]))
        if el["missing"]:
            report["missing"].append(label(el))
        if entry.get("deactivated"):
            ops.append({"op": "set_visibility", "name": cname, "value": CONTAINER_VISIBILITY})
            report["warnings"].append("%s：之前因产物里没有而隐藏，这次产物里又有了，已恢复显示" % label(el))
            entry["deactivated"] = False
        tex = element_texture(doc, el)
        if vname is None and entry.get("missingAtCreate") and not el["missing"]:
            vname = view_name(el)
            ops.append({"op": "add_view", "name": vname, "container": cname, "cls": view_class(el["type"]),
                        "texture": tex, "element": el})
            entry["view"], entry["missingAtCreate"] = vname, False
            current[eid] = (cname, vname)
        elif vname is not None and tex is not None:
            ops.append({"op": "set_texture", "view": vname, "cls": view_class(entry["type"]), "texture": tex,
                        "w": el["rect"][2], "h": el["rect"][3]})

        parent_el = doc["by_id"][el["parent"]] if el["parent"] else None
        offsets = slot_offsets(el, parent_el)
        target = layout_parent_name(el)
        actual = tree_parent.get(cname)
        if actual != target:
            if actual != recorded_parent_name(entry):
                ops.append({"op": "set_size", "name": cname, "w": offsets[2], "h": offsets[3]})
                report["moved"].append(label(el))
                new_entries[eid] = entry
                continue
            ops.append({"op": "move", "name": cname, "parent": target, "after": previous_sibling(el)})
            tree_parent[cname] = target
        entry["layoutParentId"] = el["parent"] or ROOT_PARENT
        ops.append({"op": "set_offsets", "name": cname, "offsets": offsets})
        report["updated"].append(label(el))
        new_entries[eid] = entry

    extras = []
    for eid, entry in entries.items():
        if eid in doc["by_id"] or eid not in current:
            continue
        ops.append({"op": "set_visibility", "name": current[eid][0], "value": "Collapsed"})
        entry["deactivated"] = True
        report["deactivated"].append("%s %s" % (eid, entry["sourceName"]))
        extras.append(entry)

    rec = dict(record)
    rec["elements"] = [new_entries[el["id"]] for el in doc["elements"]] + extras
    textures = texture_list(doc, hashes)
    old = record.get("textures", {})
    for t in textures:
        t["previous_hash"] = old.get(t["name"])
    rec["textures"] = {t["name"]: t["hash"] for t in textures}
    return {"ops": ops, "report": report, "record": rec, "textures": textures}
