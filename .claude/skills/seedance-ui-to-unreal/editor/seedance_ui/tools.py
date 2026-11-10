# 工具层：唯一调用 MCP 工具的地方。沙箱里 execute_tool 是注入的全局函数；测试里把 tools.execute_tool 设成假编辑器的。
# 注意：沙箱里工具报错会直接中止整个脚本、无法捕获，所以检查一律用不会报错的查询（见 docs/engine/unreal.md）。
# 工具名、参数写法以 docs/engine/unreal.md 的「MCP 工具速查」为准。

import json

from .errors import UiImportError

T_ASSET = "editor_toolset.toolsets.asset.AssetTools."
T_OBJECT = "editor_toolset.toolsets.object.ObjectTools."
T_TEXTURE = "editor_toolset.toolsets.texture.TextureTools."
T_UMG = "UMGToolSet.UMGToolSet."
T_EDITOR = "EditorToolset.EditorAppToolset."

TEXTURE_SETTINGS = {"compressionSettings": "TC_EditorIcon", "lODGroup": "TEXTUREGROUP_UI", "mipGenSettings": "TMGS_NoMipmaps"}
DEFAULT_FONT = "/Engine/EngineFonts/Roboto"

# 控件名 → 槽的 refPath（AddWidget / MoveWidget / GetWidgets 返回时记下，设槽属性时用）
SLOT_CACHE = {}


def call(tool_name_, **kw):
    return execute_tool(tool_name_, json.dumps(kw))["returnValue"]  # noqa: F821  沙箱注入


def object_path(asset_path):
    """/Game/X/WBP_X → /Game/X/WBP_X.WBP_X"""
    return "%s.%s" % (asset_path, asset_path.rsplit("/", 1)[-1])


def widget_ref(wbp, name):
    return {"refPath": "%s:WidgetTree.%s" % (object_path(wbp), name)}


def is_pie_running():
    return bool(call(T_EDITOR + "IsPIERunning"))


def asset_exists(path):
    return bool(call(T_ASSET + "exists", path=path))


def can_edit(path):
    return bool(call(T_ASSET + "can_edit_asset", asset_path=path))


def asset_class(path):
    return call(T_ASSET + "get_asset_class", asset_path=path)


def referencers(path):
    return list(call(T_ASSET + "get_referencers", asset_path=path) or [])


def delete_asset(path):
    if not call(T_ASSET + "delete", path=path):
        raise UiImportError("删除资产失败：%s" % path)


def list_assets(folder):
    return list(call(T_ASSET + "find_assets", folder_path=folder, name="", recursive=False) or [])


def save_assets(paths):
    if not call(T_ASSET + "save_assets", asset_paths=list(paths)):
        raise UiImportError("保存资产失败：%s" % "、".join(paths))


def font_typefaces(font_path):
    ref = {"refPath": object_path(font_path or DEFAULT_FONT)}
    cf = json.loads(call(T_OBJECT + "get_properties", instance=ref, properties=["compositeFont"]))["compositeFont"]
    return [f["name"] for f in cf["defaultTypeface"]["fonts"]]


def import_texture(folder, name, source_file):
    call(T_TEXTURE + "import_file", folder_path=folder, asset_name=name, source_file=source_file)
    path = "%s/%s" % (folder, name)
    call(T_OBJECT + "set_properties", instance={"refPath": object_path(path)}, values=json.dumps(TEXTURE_SETTINGS))
    return path


def create_wbp(folder, name):
    call(T_UMG + "CreateWidgetBlueprint", folderPath=folder, assetName=name,
         parentClass={"refPath": "/Script/UMG.UserWidget"})
    return "%s/%s" % (folder, name)


def read_tree(wbp):
    """{name: {"class", "parent"(根为 None), "index"}}，同级按槽位顺序"""
    res = call(T_UMG + "GetWidgets", widgetBlueprint={"refPath": object_path(wbp)})
    out = {}
    counts = {}
    for w in res["widgets"]:
        name = w["widgetName"]
        parent = w["parent"]
        pname = parent["refPath"].rsplit(".", 1)[-1] if isinstance(parent, dict) else None
        out[name] = {"class": w["widgetClassPath"]["refPath"], "parent": pname, "index": counts.get(pname, 0)}
        counts[pname] = counts.get(pname, 0) + 1
        if isinstance(w.get("slot"), dict):
            SLOT_CACHE[name] = w["slot"]["refPath"]
    return out


def add_widget(wbp, cls, name, parent, index=-1):
    kw = {"widgetBlueprint": {"refPath": object_path(wbp)}, "widgetClass": {"refPath": cls},
          "widgetDisplayName": name, "childIndex": index}
    if parent is not None:
        kw["parentWidget"] = widget_ref(wbp, parent)
    res = call(T_UMG + "AddWidget", **kw)
    if res.get("widgetName") != name:
        raise UiImportError("加控件 %s 失败（得到的名字是 %s，可能已有同名控件）" % (name, res.get("widgetName")))
    if isinstance(res.get("slot"), dict):
        SLOT_CACHE[name] = res["slot"]["refPath"]


def move_widget(wbp, name, parent, index):
    res = call(T_UMG + "MoveWidget", widgetBlueprint={"refPath": object_path(wbp)}, widget=widget_ref(wbp, name),
               newParent=widget_ref(wbp, parent), childIndex=index)
    if isinstance(res.get("slot"), dict):
        SLOT_CACHE[name] = res["slot"]["refPath"]


def set_widget(wbp, name, values):
    if not call(T_OBJECT + "set_properties", instance=widget_ref(wbp, name), values=json.dumps(values)):
        raise UiImportError("设置控件 %s 的属性失败：%s" % (name, ", ".join(sorted(values))))


def set_slot(wbp, name, values):
    ref = SLOT_CACHE.get(name)
    if ref is None:
        raise UiImportError("找不到控件 %s 的槽" % name)
    if not call(T_OBJECT + "set_properties", instance={"refPath": ref}, values=json.dumps(values)):
        raise UiImportError("设置控件 %s 的槽属性失败：%s" % (name, ", ".join(sorted(values))))


BRUSH_PROPERTIES = {"/Script/UMG.Image": "brush", "/Script/UMG.Button": "widgetStyle",
                    "/Script/UMG.CheckBox": "widgetStyle", "/Script/UMG.ProgressBar": "widgetStyle"}


def widget_texture_refs(wbp, name, cls):
    """控件笔刷 / 样式里引用的对象路径集合（非笔刷类控件返回空集）"""
    prop = BRUSH_PROPERTIES.get(cls)
    if prop is None:
        return set()
    values = json.loads(call(T_OBJECT + "get_properties", instance=widget_ref(wbp, name), properties=[prop]))
    found = set()
    stack = [values]
    while stack:
        v = stack.pop()
        if isinstance(v, dict):
            if isinstance(v.get("refPath"), str):
                found.add(v["refPath"])
            stack.extend(v.values())
        elif isinstance(v, list):
            stack.extend(v)
    return found


def compile_wbp(wbp):
    if not call(T_UMG + "CompileWidgetBlueprint", widgetBlueprint={"refPath": object_path(wbp)}):
        raise UiImportError("Widget Blueprint 编译失败：%s（没有保存）" % wbp)
