# 导入记录：<工程>/Content/SeedanceKlab/UI/<WBP 路径去掉 /Game/>.json（AssetTools.read_file / write_file 读写）。
# 重导靠它把控件和 layout.json 的元素对上（UMG 没有稳定 id，只能按控件名认），并记住切图哈希。

import json

from .errors import UiImportError
from .naming import container_name, view_name

FORMAT_VERSION = 1
ROOT_PARENT = "<root>"
ROOT_WIDGETS = {"root": "Root", "scaleBox": "ScaleBox", "sizeBox": "SizeBox", "design": "Design"}
ENTRY_KEYS = ("elementId", "container", "view", "sourceName", "type", "layoutParentId", "missingAtCreate")


def new_record(wbp, doc):
    return {
        "formatVersion": FORMAT_VERSION,
        "widgetBlueprint": wbp,
        "designWidth": doc["width"],
        "designHeight": doc["height"],
        "layoutVersion": doc["version"],
        "rootWidgets": dict(ROOT_WIDGETS),
        "textures": {},
        "elements": [],
    }


def element_entry(el):
    """首次建节点时的记录条目（显示控件名在 missing 时为 None）"""
    return {
        "elementId": el["id"],
        "container": container_name(el),
        "view": view_name(el),
        "sourceName": el["name"],
        "type": el["type"],
        "layoutParentId": el["parent"] or ROOT_PARENT,
        "missingAtCreate": bool(el["missing"]),
    }


def dump_record(rec):
    return json.dumps(rec, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def load_record(text, wbp):
    try:
        rec = json.loads(text)
    except ValueError:
        rec = None
    bad = "导入记录损坏，读不出来（%s 的记录）。从版本库恢复它，或删掉它和对应的 Widget Blueprint 再导入" % wbp
    if not isinstance(rec, dict):
        raise UiImportError(bad)
    if rec.get("formatVersion") != FORMAT_VERSION:
        raise UiImportError("导入记录的 formatVersion 是 %s，这个版本只认 %d：%s" % (rec.get("formatVersion"), FORMAT_VERSION, wbp))
    if rec.get("widgetBlueprint") != wbp:
        raise UiImportError("导入记录指向的是 %s，不是 %s。Widget Blueprint 挪过位置的话，把记录一起挪到对应路径"
                            % (rec.get("widgetBlueprint"), wbp))
    if not isinstance(rec.get("elements"), list) or not isinstance(rec.get("textures"), dict) \
            or not isinstance(rec.get("rootWidgets"), dict):
        raise UiImportError(bad)
    for e in rec["elements"]:
        if not isinstance(e, dict) or not e.get("elementId") or not e.get("container") or any(k not in e for k in ENTRY_KEYS):
            raise UiImportError(bad)
    for k in ROOT_WIDGETS:
        if not rec["rootWidgets"].get(k):
            raise UiImportError(bad)
    return rec
