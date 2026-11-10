# 执行导入：先检查（任何一项不过就抛错，这时什么都没写）→ 贴图 → 建 / 改控件树 → 编译 → 保存。
# 记录文件由本地启动器写（沙箱里写文件出错无法捕获），这里只返回记录文本。

from .errors import UiImportError
from .naming import CONTAINER_CLASS, texture_folder
from .geometry import font_size, justification, parse_color, text_anchor
from .planner import CONTAINER_VISIBILITY, label
from .tools import add_widget, asset_class, asset_exists, can_edit, delete_asset, import_texture, is_pie_running, move_widget, object_path, referencers, set_slot, set_widget  # noqa: E501

HOVER_TINT = 1.1
PRESS_TINT = 0.85
STRETCH = {"anchors": {"minimum": {"x": 0, "y": 0}, "maximum": {"x": 1, "y": 1}},
           "offsets": {"left": 0, "top": 0, "right": 0, "bottom": 0}, "alignment": {"x": 0, "y": 0}}
ROOT_CLASSES = (("root", CONTAINER_CLASS), ("scaleBox", "/Script/UMG.ScaleBox"), ("sizeBox", "/Script/UMG.SizeBox"),
                ("design", CONTAINER_CLASS))


def texture_path(import_dir, name):
    return "%s/%s" % (texture_folder(import_dir), name)


def textures_to_replace(import_dir, textures):
    """已存在、且内容（哈希）变了或记录里没有它的贴图"""
    out = []
    for t in textures:
        path = texture_path(import_dir, t["name"])
        if asset_exists(path) and (t.get("previous_hash") is None or t["previous_hash"] != t["hash"]):
            out.append(path)
    return out


def preflight(wbp, wbp_exists, record, font):
    if is_pie_running():
        raise UiImportError("编辑器正在 PIE（运行游戏），先停止再导入；这时什么都还没写")
    if wbp_exists and record is None:
        raise UiImportError("%s 已存在但找不到它的导入记录。删掉这个 Widget Blueprint 再导入会按首次导入重建（程序加的内容会丢）" % wbp)
    if record is not None and not wbp_exists:
        raise UiImportError("有 %s 的导入记录，但 Widget Blueprint 不在了。从版本库恢复它，或删掉导入记录后按首次导入" % wbp)
    if font and (not asset_exists(font) or asset_class(font) != "Font"):
        raise UiImportError("找不到字体资产：%s" % font)
    if wbp_exists and not can_edit(wbp):
        raise UiImportError("%s 不能编辑（可能需要先在版本管理里 checkout）；这时什么都还没写" % wbp)


def check_textures_editable(paths):
    locked = [p for p in paths if not can_edit(p)]
    if locked:
        raise UiImportError("这些贴图不能编辑（可能需要先 checkout）：%s；这时什么都还没写" % "、".join(locked))


def update_textures(import_dir, wbp, textures, record, report, slice_root):
    """导入新贴图 / 替换内容变了的贴图。有外部引用的不替换，记录保留旧哈希。返回导入或替换过的资产路径"""
    touched = []
    folder = texture_folder(import_dir)
    for t in textures:
        path = texture_path(import_dir, t["name"])
        source = t["source_image"]
        if asset_exists(path):
            if t.get("previous_hash") is not None and t["previous_hash"] == t["hash"]:
                continue
            others = [r for r in referencers(path) if r != wbp]
            if others:
                report["warnings"].append("%s 被 %s 引用，切图已更新但没有替换，请手动处理" % (t["name"], "、".join(others)))
                record["textures"][t["name"]] = t.get("previous_hash")
                continue
            delete_asset(path)
        import_texture(folder, t["name"], "%s/%s" % (slice_root.rstrip("/"), source))
        touched.append(path)
    return touched



def tint(k):
    return {"specifiedColor": {"r": k, "g": k, "b": k, "a": 1}, "colorUseRule": "UseColor_Specified"}


def brush(import_dir, texture, w, h, k=1.0):
    path = texture_path(import_dir, texture)
    return {"resourceObject": {"refPath": object_path(path)}, "imageSize": {"x": w, "y": h},
            "drawAs": "Image", "tintColor": tint(k)}


def view_values(import_dir, cls, texture, w, h):
    """贴图相关的属性（首建与重导共用）"""
    if texture is None:
        return {}
    b = lambda k=1.0: brush(import_dir, texture, w, h, k)
    if cls.endswith(".Button"):
        return {"widgetStyle": {"normal": b(), "hovered": b(HOVER_TINT), "pressed": b(PRESS_TINT)}}
    if cls.endswith(".CheckBox"):
        return {"widgetStyle": {"checkBoxType": "ToggleButton",
                                "uncheckedImage": b(), "uncheckedHoveredImage": b(HOVER_TINT), "uncheckedPressedImage": b(PRESS_TINT),
                                "checkedImage": b(), "checkedHoveredImage": b(HOVER_TINT), "checkedPressedImage": b(PRESS_TINT)}}
    if cls.endswith(".ProgressBar"):
        return {"widgetStyle": {"fillImage": b(), "backgroundImage": {"drawAs": "NoDrawType"}}}
    if cls.endswith(".Image"):
        return {"brush": b()}
    return {}


class TreeMirror:
    """执行过程中维护的控件树（算插入下标用）"""

    def __init__(self, tree, views):
        self.kids = {}
        for name, w in sorted(tree.items(), key=lambda kv: kv[1]["index"]):
            self.kids.setdefault(w["parent"], []).append(name)
        self.parent = {n: w["parent"] for n, w in tree.items()}
        self.views = views

    def index_after(self, parent, after):
        kids = self.kids.setdefault(parent, [])
        if after is None:
            return 1 if kids and kids[0] in self.views else 0
        return kids.index(after) + 1

    def insert(self, name, parent, index):
        if name in self.parent:
            self.kids[self.parent[name]].remove(name)
        kids = self.kids.setdefault(parent, [])
        kids.insert(len(kids) if index < 0 else index, name)
        self.parent[name] = parent


class Executor:
    def __init__(self, wbp, import_dir, font, faces, report, mirror):
        self.wbp, self.import_dir, self.font, self.faces = wbp, import_dir, font, faces
        self.report, self.mirror = report, mirror

    def run(self, ops, roots, width, height):
        for o in ops:
            getattr(self, "op_" + o["op"])(o, roots, width, height)

    def add(self, cls, name, parent, index):
        add_widget(self.wbp, cls, name, parent, index)
        self.mirror.insert(name, parent, index)

    def op_ensure_root(self, o, roots, width, height):
        if roots["root"] in self.mirror.parent:
            return
        names = [roots[k] for k, _ in ROOT_CLASSES]
        parent = None
        for (key, cls), name in zip(ROOT_CLASSES, names):
            self.add(cls, name, parent, -1)
            parent = name
        set_slot(self.wbp, roots["scaleBox"], {"layoutData": STRETCH})
        set_widget(self.wbp, roots["scaleBox"], {"stretch": "ScaleToFit"})
        set_widget(self.wbp, roots["sizeBox"], {"widthOverride": width, "heightOverride": height,
                                                      "bOverride_WidthOverride": True, "bOverride_HeightOverride": True})

    def op_add_container(self, o, *_):
        index = self.mirror.index_after(o["parent"], o["after"]) if "after" in o else -1
        self.add(CONTAINER_CLASS, o["name"], o["parent"], index)
        l, t, w, h = o["offsets"]
        set_slot(self.wbp, o["name"], {"layoutData": {
            "anchors": {"minimum": {"x": 0, "y": 0}, "maximum": {"x": 0, "y": 0}},
            "offsets": {"left": l, "top": t, "right": w, "bottom": h}, "alignment": {"x": 0, "y": 0}}, "bAutoSize": False})
        set_widget(self.wbp, o["name"], {"visibility": CONTAINER_VISIBILITY})

    def op_add_view(self, o, *_):
        el = o["element"]
        cls = o["cls"]
        self.add(cls, o["name"], o["container"], 0)
        w, h = el["rect"][2], el["rect"][3]
        if cls.endswith(".TextBlock"):
            self.text(o["name"], el)
            return
        set_slot(self.wbp, o["name"], {"layoutData": STRETCH})
        values = view_values(self.import_dir, cls, o["texture"], w, h)
        if o["texture"] is None:
            self.report["warnings"].append("%s：没有切图" % label(el))
        if not cls.endswith(".Button") and not cls.endswith(".CheckBox"):
            values["visibility"] = "HitTestInvisible"
        if cls.endswith(".ProgressBar"):
            values.update({"percent": 1.0, "barFillType": "LeftToRight"})
        set_widget(self.wbp, o["name"], values)

    def text(self, name, el):
        t = el["text"]
        size, warn = font_size(t["font_size"])
        if warn:
            self.report["warnings"].append("%s：%s" % (label(el), warn))
        rgba, warn = parse_color(t["color"])
        if warn:
            self.report["warnings"].append("%s：%s" % (label(el), warn))
        face = "Regular"
        if t["bold"]:
            if "Bold" in self.faces:
                face = "Bold"
            else:
                self.report["warnings"].append("%s：字体没有 Bold 字形，用了常规字形" % label(el))
        font = {"size": size, "typefaceFontName": face}
        if self.font:
            font["fontObject"] = {"refPath": object_path(self.font)}
        r, g, b, a = rgba
        set_widget(self.wbp, name, {
            "text": t["content"], "justification": justification(t["align"]), "font": font,
            "colorAndOpacity": {"specifiedColor": {"r": r, "g": g, "b": b, "a": a}, "colorUseRule": "UseColor_Specified"},
            "visibility": "HitTestInvisible"})
        ax, ay = text_anchor(t["align"])
        set_slot(self.wbp, name, {"layoutData": {
            "anchors": {"minimum": {"x": ax, "y": ay}, "maximum": {"x": ax, "y": ay}},
            "alignment": {"x": ax, "y": ay}, "offsets": {"left": 0, "top": 0, "right": 0, "bottom": 0}}, "bAutoSize": True})

    def op_set_texture(self, o, *_):
        values = view_values(self.import_dir, o["cls"], o["texture"], o["w"], o["h"])
        if values:
            set_widget(self.wbp, o["view"], values)

    def op_set_offsets(self, o, *_):
        l, t, w, h = o["offsets"]
        set_slot(self.wbp, o["name"], {"layoutData": {"offsets": {"left": l, "top": t, "right": w, "bottom": h}}})

    def op_set_size(self, o, *_):
        set_slot(self.wbp, o["name"], {"layoutData": {"offsets": {"right": o["w"], "bottom": o["h"]}}})

    def op_move(self, o, *_):
        index = self.mirror.index_after(o["parent"], o["after"])
        move_widget(self.wbp, o["name"], o["parent"], index)
        self.mirror.insert(o["name"], o["parent"], index)

    def op_set_visibility(self, o, *_):
        set_widget(self.wbp, o["name"], {"visibility": o["value"]})
