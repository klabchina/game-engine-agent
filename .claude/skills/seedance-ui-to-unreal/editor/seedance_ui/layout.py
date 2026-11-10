# ui-maker 的 layout.json（version 1）→ 规整后的文档。
# 坐标是设计分辨率下的画布绝对像素，原点左上、y 向下；elements 的数组顺序就是叠放顺序。
# 元素：{id, name, type, image, parent, same_as, rect: (x, y, w, h), missing, text}
#   missing 元素的 rect 取 drawnRect；text 只有 type == "text" 的元素有：{content, font_size, color, align, bold}

import json

from .errors import UiImportError

LAYOUT_VERSION = 1


def _rect(r):
    if not isinstance(r, dict):
        return None
    try:
        return (int(r["x"]), int(r["y"]), int(r["w"]), int(r["h"]))
    except (KeyError, TypeError, ValueError):
        return None


def _element(raw, index):
    if not isinstance(raw, dict) or not raw.get("id"):
        raise UiImportError("layout.json 里第 %d 个元素没有 id" % (index + 1))
    eid = str(raw["id"])
    etype = str(raw.get("type") or "")
    missing = bool(raw.get("missing"))
    rect = _rect(raw.get("drawnRect")) if missing else _rect(raw.get("rect"))
    if rect is None:
        raise UiImportError("元素 %s 没有位置（rect / drawnRect）" % eid)
    text = None
    if etype == "text":
        t = raw.get("text") if isinstance(raw.get("text"), dict) else {}
        text = {"content": t.get("content") or "", "font_size": t.get("fontSize"), "color": t.get("color"),
                "align": t.get("align") or "center", "bold": bool(t.get("bold"))}
    return {
        "id": eid,
        "name": str(raw.get("name") or ""),
        "type": etype,
        "image": raw.get("image") or None,
        "parent": raw.get("parent") or None,
        "same_as": raw.get("sameAs") or None,
        "rect": rect,
        "missing": missing,
        "text": text,
    }


def parse_layout(text):
    try:
        raw = json.loads(text)
    except ValueError as ex:
        raise UiImportError("layout.json 不是合法的 JSON：%s" % ex)
    if not isinstance(raw, dict):
        raise UiImportError("layout.json 不是合法的 JSON 对象")
    if raw.get("version") != LAYOUT_VERSION:
        raise UiImportError("layout.json 的 version 是 %s，只支持 %d" % (raw.get("version"), LAYOUT_VERSION))
    res = raw.get("resolution")
    width = res.get("width") if isinstance(res, dict) else None
    height = res.get("height") if isinstance(res, dict) else None
    if not isinstance(width, int) or not isinstance(height, int) or width <= 0 or height <= 0:
        raise UiImportError("layout.json 缺少 resolution（设计分辨率）")

    elements = [_element(e, i) for i, e in enumerate(raw.get("elements") or [])]
    by_id = {}
    for el in elements:
        if el["id"] in by_id:
            raise UiImportError("layout.json 里元素 id %s 重复" % el["id"])
        by_id[el["id"]] = el
    for el in elements:
        if el["parent"] is not None and el["parent"] not in by_id:
            raise UiImportError("元素 %s 的 parent %s 不存在" % (el["id"], el["parent"]))
        if el["same_as"] is not None and el["same_as"] not in by_id:
            raise UiImportError("元素 %s 的 sameAs %s 不存在" % (el["id"], el["same_as"]))
    for el in elements:
        seen = set()
        cur = el
        while cur["parent"] is not None:
            if cur["id"] in seen:
                raise UiImportError("元素 %s 的父子关系成环" % el["id"])
            seen.add(cur["id"])
            cur = by_id[cur["parent"]]
    return {"version": LAYOUT_VERSION, "width": width, "height": height, "elements": elements, "by_id": by_id}
