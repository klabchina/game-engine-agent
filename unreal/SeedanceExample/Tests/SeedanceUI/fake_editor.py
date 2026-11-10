"""测试用假编辑器：在 execute_tool 层按完整工具名模拟 MCP 工具（只模拟导入用到的那些）。

用法：
    fake = FakeEditor()
    fake.install(self)          # 把 seedance_ui.tools.execute_tool 换成 fake.execute_tool，测试结束自动还原
开关：fake.pie / fake.locked（不可编辑的资产路径集合）/ fake.fail_on（这些工具短名直接抛 RuntimeError，模拟沙箱中止）
      fake.compile_ok / fake.external_refs（{贴图路径: [额外引用者]}）/ fake.files（磁盘上存在的源图路径集合，None = 都存在）
"""

import copy
import json
from unittest import mock

from seedance_ui import tools

DEFAULT_FONT_FACES = ["Regular", "Bold", "Italic", "Bold Italic", "Light"]


def short(name):
    return name.rsplit(".", 1)[-1]


def asset_of(ref_path):
    """/Game/X/WBP_X.WBP_X:WidgetTree.A → /Game/X/WBP_X"""
    return ref_path.split(":", 1)[0].rsplit(".", 1)[0]


class FakeWbp:
    def __init__(self):
        self.cls = {}
        self.parent = {}
        self.children = {None: []}
        self.props = {}
        self.slot_props = {}
        self.slot_id = {}
        self.next_slot = 0

    def add(self, cls, name, parent, index):
        if name in self.cls:
            name = name + "_0"  # 真编辑器遇到重名会自动改名
        self.cls[name] = cls
        self.parent[name] = parent
        self.children.setdefault(name, [])
        kids = self.children.setdefault(parent, [])
        kids.insert(len(kids) if index is None or index < 0 else index, name)
        self.props[name] = {}
        self.slot_props[name] = {}
        self.slot_id[name] = "Slot_%d" % self.next_slot
        self.next_slot += 1
        return name

    def move(self, name, parent, index):
        self.children[self.parent[name]].remove(name)
        self.parent[name] = parent
        kids = self.children.setdefault(parent, [])
        kids.insert(len(kids) if index < 0 else index, name)
        self.slot_id[name] = "Slot_%d" % self.next_slot
        self.next_slot += 1

    def walk(self):
        out = []

        def rec(p):
            for c in self.children.get(p, []):
                out.append(c)
                rec(c)
        rec(None)
        return out

    def rename(self, old, new):
        for d in (self.cls, self.parent, self.props, self.slot_props, self.slot_id):
            d[new] = d.pop(old)
        kids = self.children[self.parent[new]]
        kids[kids.index(old)] = new
        self.children[new] = self.children.pop(old, [])
        for c in self.children[new]:
            self.parent[c] = new


def merge(dst, src):
    for k, v in src.items():
        if isinstance(v, dict) and isinstance(dst.get(k), dict):
            merge(dst[k], v)
        else:
            dst[k] = copy.deepcopy(v)


class FakeEditor:
    def __init__(self):
        self.pie = False
        self.locked = set()
        self.fail_on = set()
        self.compile_ok = True
        self.external_refs = {}
        self.font_faces = {"/Engine/EngineFonts/Roboto": list(DEFAULT_FONT_FACES)}
        self.assets = {"/Engine/EngineFonts/Roboto": {"class": "Font", "props": {}}}
        self.wbps = {}
        self.saved = []
        self.calls = []

    # ---- 便于测试读状态 ----
    WRITE_TOOLS = {"import_file", "delete", "save_assets", "CreateWidgetBlueprint", "AddWidget", "MoveWidget",
                   "set_properties", "CompileWidgetBlueprint"}

    def write_calls(self):
        return [c for c in self.calls if c[0] in self.WRITE_TOOLS]

    def count(self, tool):
        return sum(1 for c in self.calls if c[0] == tool)

    def wbp(self, path):
        return self.wbps[path]

    def install(self, test):
        p = mock.patch.object(tools, "execute_tool", self.execute_tool, create=True)
        p.start()
        test.addCleanup(p.stop)
        tools.SLOT_CACHE.clear()
        test.addCleanup(tools.SLOT_CACHE.clear)
        return self

    # ---- 工具分派 ----
    def execute_tool(self, name, args_json):
        args = json.loads(args_json)
        s = short(name)
        self.calls.append((s, args))
        if s in self.fail_on:
            raise RuntimeError("Script error in %s: injected failure" % name)
        fn = getattr(self, "t_" + s, None)
        if fn is None:
            raise RuntimeError("FakeEditor 没有模拟工具 %s" % name)
        return {"returnValue": fn(**args)}

    def t_IsPIERunning(self):
        return self.pie

    def t_exists(self, path):
        return path in self.assets or path in self.wbps

    def t_can_edit_asset(self, asset_path):
        return asset_path not in self.locked

    def t_get_asset_class(self, asset_path):
        if asset_path in self.wbps:
            return "WidgetBlueprint"
        return self.assets[asset_path]["class"]

    def t_get_referencers(self, asset_path):
        needle = '"%s.' % asset_path
        refs = [w for w, t in self.wbps.items() if needle in json.dumps(t.props)]
        return refs + list(self.external_refs.get(asset_path, []))

    def t_delete(self, path):
        self.assets.pop(path, None)
        needle = "%s." % path
        for t in self.wbps.values():
            for props in t.props.values():
                self.null_refs(props, needle)
        return True

    def null_refs(self, obj, needle):
        for k, v in list(obj.items()):
            if isinstance(v, dict):
                if str(v.get("refPath", "")).startswith(needle):
                    obj[k] = "None"
                else:
                    self.null_refs(v, needle)

    def t_find_assets(self, folder_path, name, recursive=True):
        pre = folder_path.rstrip("/") + "/"
        return sorted(p for p in list(self.assets) + list(self.wbps)
                      if p.startswith(pre) and (recursive or "/" not in p[len(pre):]))

    def t_save_assets(self, asset_paths):
        self.saved.append(list(asset_paths))
        return True

    def t_import_file(self, folder_path, asset_name, source_file):
        path = "%s/%s" % (folder_path, asset_name)
        if path in self.assets:
            raise RuntimeError("import_asset: %s at %s already exists" % (asset_name, folder_path))
        self.assets[path] = {"class": "Texture2D", "props": {}, "source": source_file}
        return [{"refPath": tools.object_path(path)}]

    def t_CreateWidgetBlueprint(self, folderPath, assetName, parentClass):
        path = "%s/%s" % (folderPath, assetName)
        self.wbps[path] = FakeWbp()
        return {"refPath": tools.object_path(path)}

    def widget_info(self, wbp_path, t, name):
        op = tools.object_path(wbp_path)
        parent = t.parent[name]
        return {"widget": {"refPath": "%s:WidgetTree.%s" % (op, name)},
                "parent": {"refPath": "%s:WidgetTree.%s" % (op, parent)} if parent else "None",
                "slot": {"refPath": "%s:WidgetTree.%s.%s" % (op, parent, t.slot_id[name])} if parent else "None",
                "widgetName": name, "widgetClassPath": {"refPath": t.cls[name]}}

    def name_of(self, ref):
        return ref["refPath"].rsplit(".", 1)[-1]

    def t_GetWidgets(self, widgetBlueprint):
        path = asset_of(widgetBlueprint["refPath"])
        t = self.wbps[path]
        return {"info": {"widgetCount": len(t.cls)}, "widgets": [self.widget_info(path, t, n) for n in t.walk()]}

    def t_AddWidget(self, widgetBlueprint, widgetClass, widgetDisplayName, parentWidget=None, childIndex=-1):
        path = asset_of(widgetBlueprint["refPath"])
        t = self.wbps[path]
        parent = self.name_of(parentWidget) if parentWidget else None
        if parent is None and t.children[None]:
            raise RuntimeError("树已有根")
        name = t.add(widgetClass["refPath"], widgetDisplayName, parent, childIndex)
        return self.widget_info(path, t, name)

    def t_MoveWidget(self, widgetBlueprint, widget, newParent, childIndex=-1):
        path = asset_of(widgetBlueprint["refPath"])
        t = self.wbps[path]
        name = self.name_of(widget)
        t.move(name, self.name_of(newParent), childIndex)
        return self.widget_info(path, t, name)

    def t_set_properties(self, instance, values):
        ref = instance["refPath"]
        vals = json.loads(values)
        if ":WidgetTree." in ref:
            path = asset_of(ref)
            t = self.wbps[path]
            tail = ref.split(":WidgetTree.", 1)[1].split(".")
            if len(tail) == 1:
                merge(t.props[tail[0]], vals)
            else:
                owner = next(n for n, sid in t.slot_id.items() if sid == tail[-1] and t.parent[n] == tail[-2])
                merge(t.slot_props[owner], vals)
            return True
        merge(self.assets[asset_of(ref)]["props"], vals)
        return True

    def t_get_properties(self, instance, properties):
        path = asset_of(instance["refPath"])
        if "compositeFont" in properties:
            faces = self.font_faces.get(path, ["Regular"])
            return json.dumps({"compositeFont": {"defaultTypeface": {"fonts": [{"name": f} for f in faces]}}})
        ref = instance["refPath"]
        if ":WidgetTree." in ref:
            t = self.wbps[path]
            name = ref.split(":WidgetTree.", 1)[1]
            return json.dumps({p: t.props[name].get(p) for p in properties})
        raise RuntimeError("FakeEditor: get_properties 只模拟了 compositeFont 与控件属性")

    def t_CompileWidgetBlueprint(self, widgetBlueprint):
        return self.compile_ok
