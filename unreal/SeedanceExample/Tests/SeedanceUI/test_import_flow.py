import copy
import json
import unittest

from fake_editor import FakeEditor
from fixtures import layout_dict, layout_text
from seedance_ui import main
from seedance_ui.errors import UiImportError
from seedance_ui.record import load_record

IMPORT_DIR = "/Game/Tests/X"
WBP = "/Game/Tests/X/WBP_X"
SLICES = "C:/tmp/slices-root"


def request(name="ui-status", layout=None, record_text=None, font="", hashes=None):
    raw = layout if layout is not None else layout_dict(name)
    if hashes is None:
        hashes = {e["image"]: "h-" + e["image"] for e in raw["elements"] if e.get("image")}
    return {"importDir": IMPORT_DIR, "wbpName": "", "font": font, "layoutJson": json.dumps(raw, ensure_ascii=False),
            "sliceRoot": SLICES, "hashes": hashes, "recordText": record_text}


class FlowTests(unittest.TestCase):
    def setUp(self):
        self.fake = FakeEditor().install(self)

    def run_import(self, req):
        main.REQUEST = req
        self.addCleanup(lambda: setattr(main, "REQUEST", None))
        return main.run()

    def first(self, name="ui-status", **kw):
        return self.run_import(request(name, **kw))

    def test_first_import_ui_status(self):
        out = self.first()
        rep = out["report"]
        self.assertEqual(len(rep["created"]), 65)
        self.assertEqual(rep["widgetBlueprint"], WBP)
        t = self.fake.wbp(WBP)
        self.assertEqual([t.parent[n] for n in ("Root", "ScaleBox", "SizeBox", "Design")], [None, "Root", "ScaleBox", "SizeBox"])
        self.assertEqual(t.props["SizeBox"]["widthOverride"], 2560)
        self.assertEqual(t.props["SizeBox"]["heightOverride"], 1440)
        self.assertEqual(t.props["ScaleBox"]["stretch"], "ScaleToFit")
        self.assertEqual(t.slot_props["button_e62"]["layoutData"]["offsets"],
                         {"left": 1367 - 237, "top": 1157 - 127, "right": 286, "bottom": 105})
        self.assertEqual(t.props["button_e62"]["visibility"], "SelfHitTestInvisible")
        self.assertEqual(t.props["panel_e1_Image"]["visibility"], "HitTestInvisible")
        self.assertEqual(t.props["text_e63_Text"]["visibility"], "HitTestInvisible")
        self.assertNotIn("visibility", t.props["button_e62_Button"])
        style = t.props["button_e62_Button"]["widgetStyle"]
        self.assertEqual(style["hovered"]["tintColor"]["specifiedColor"]["r"], 1.1)
        self.assertEqual(style["pressed"]["tintColor"]["specifiedColor"]["r"], 0.85)
        self.assertEqual(style["normal"]["resourceObject"]["refPath"], "/Game/Tests/X/Textures/T_button_e62.T_button_e62")
        self.assertEqual(style["normal"]["imageSize"], {"x": 286, "y": 105})
        self.assertEqual(t.children["button_e62"][0], "button_e62_Button")
        txt = t.props["text_e3_Text"]
        self.assertEqual(txt["text"], "角色属性")
        raw = next(e for e in layout_dict("ui-status")["elements"] if e["id"] == "e3")
        self.assertEqual(txt["font"]["size"], raw["text"]["fontSize"] * 0.75)
        self.assertEqual(t.slot_props["text_e3_Text"]["bAutoSize"], True)
        pb = t.props["bar_e11_ProgressBar"]
        self.assertEqual((pb["percent"], pb["barFillType"]), (1.0, "LeftToRight"))
        self.assertEqual(t.props["tab_e9_CheckBox"]["widgetStyle"]["checkBoxType"], "ToggleButton")
        # 贴图：8 张，导入设置是 UI
        textures = [p for p in self.fake.assets if p.startswith("/Game/Tests/X/Textures/")]
        self.assertEqual(len(textures), 8)
        self.assertEqual(self.fake.assets["/Game/Tests/X/Textures/T_bar_e11"]["source"], SLICES + "/slices/属性条_1.png")
        # 编译后保存，保存列表里有 WBP 和贴图
        self.assertIn(WBP, self.fake.saved[-1])
        rec = load_record(out["recordText"], WBP)
        self.assertEqual(len(rec["elements"]), 65)
        self.assertEqual(rec["textures"]["T_bar_e11"], "h-slices/属性条_1.png")

    def test_first_import_ui_popup(self):
        out = self.first("ui-popup")
        self.assertEqual(len(out["report"]["created"]), len(layout_dict("ui-popup")["elements"]))
        self.assertEqual(out["report"]["warnings"], [])

    def test_reimport_keeps_manual_widget(self):
        out = self.first()
        self.fake.wbp(WBP).add("/Script/UMG.Button", "MyButton", "panel_e1", -1)
        before = len(self.fake.wbp(WBP).cls)
        out2 = self.run_import(request(record_text=out["recordText"]))
        t = self.fake.wbp(WBP)
        self.assertIn("MyButton", t.cls)
        self.assertEqual(len(t.cls), before)
        self.assertEqual(len(out2["report"]["updated"]), 65)
        self.assertEqual(out2["report"]["created"], [])

    def test_reimport_renamed_view_keeps_name(self):
        out = self.first()
        self.fake.wbp(WBP).rename("button_e62_Button", "UpgradeButton")
        out2 = self.run_import(request(record_text=out["recordText"]))
        self.assertEqual(out2["report"]["rebuilt"], [])
        self.assertNotIn("button_e62_Button", self.fake.wbp(WBP).cls)
        rec = load_record(out2["recordText"], WBP)
        self.assertEqual(next(e for e in rec["elements"] if e["elementId"] == "e62")["view"], "UpgradeButton")

    def test_reimport_moved_container_keeps_position(self):
        out = self.first()
        t = self.fake.wbp(WBP)
        t.move("button_e62", "panel_e8", -1)
        t.slot_props["button_e62"]["layoutData"]["offsets"]["left"] = 5
        raw = layout_dict("ui-status")
        next(e for e in raw["elements"] if e["id"] == "e62")["rect"]["w"] = 300
        out2 = self.run_import(request(layout=raw, record_text=out["recordText"]))
        self.assertIn("e62 升级", out2["report"]["moved"])
        offs = self.fake.wbp(WBP).slot_props["button_e62"]["layoutData"]["offsets"]
        self.assertEqual((offs["left"], offs["right"]), (5, 300))

    def test_reimport_upstream_reparent_moves(self):
        out = self.first()
        raw = layout_dict("ui-status")
        next(e for e in raw["elements"] if e["id"] == "e62")["parent"] = "e8"
        self.run_import(request(layout=raw, record_text=out["recordText"]))
        t = self.fake.wbp(WBP)
        self.assertEqual(t.parent["button_e62"], "panel_e8")

    def test_new_element_inserted_after_previous_sibling(self):
        out = self.first()
        raw = layout_dict("ui-status")
        idx = next(i for i, e in enumerate(raw["elements"]) if e["id"] == "e62")
        raw["elements"].insert(idx, {"id": "e99", "name": "新", "type": "icon", "image": "slices/升级.png",
                                     "rect": {"x": 300, "y": 300, "w": 10, "h": 10}, "parent": "e1"})
        self.run_import(request(layout=raw, record_text=out["recordText"]))
        kids = self.fake.wbp(WBP).children["panel_e1"]
        self.assertEqual(kids.index("icon_e99"), kids.index("panel_e44") + 1)

    def test_deactivate_sets_collapsed(self):
        out = self.first()
        raw = layout_dict("ui-status")
        raw["elements"] = [e for e in raw["elements"] if e["id"] not in ("e64", "e65")]
        self.run_import(request(layout=raw, record_text=out["recordText"]))
        self.assertEqual(self.fake.wbp(WBP).props["button_e64"]["visibility"], "Collapsed")

    def test_texture_unchanged_not_touched(self):
        out = self.first()
        n_import = self.fake.count("import_file")
        self.run_import(request(record_text=out["recordText"]))
        self.assertEqual(self.fake.count("import_file"), n_import)
        self.assertEqual(self.fake.count("delete"), 0)

    def test_texture_changed_reimported(self):
        out = self.first()
        req = request(record_text=out["recordText"])
        req["hashes"]["slices/升级.png"] = "new"
        out2 = self.run_import(req)
        self.assertEqual([c[1]["path"] for c in self.fake.calls if c[0] == "delete"],
                         ["/Game/Tests/X/Textures/T_button_e62"])
        t = self.fake.wbp(WBP)
        self.assertEqual(t.props["button_e62_Button"]["widgetStyle"]["normal"]["resourceObject"]["refPath"],
                         "/Game/Tests/X/Textures/T_button_e62.T_button_e62")
        # e64 与 e62 同款，也要重设笔刷
        self.assertEqual(t.props["button_e64_Button"]["widgetStyle"]["normal"]["resourceObject"]["refPath"],
                         "/Game/Tests/X/Textures/T_button_e62.T_button_e62")
        self.assertEqual(load_record(out2["recordText"], WBP)["textures"]["T_button_e62"], "new")

    def test_texture_changed_external_ref_skipped(self):
        out = self.first()
        self.fake.external_refs["/Game/Tests/X/Textures/T_button_e62"] = ["/Game/Other/WBP_Other"]
        req = request(record_text=out["recordText"])
        req["hashes"]["slices/升级.png"] = "new"
        out2 = self.run_import(req)
        self.assertEqual(self.fake.count("delete"), 0)
        self.assertTrue(any("T_button_e62" in w and "WBP_Other" in w for w in out2["report"]["warnings"]))
        self.assertEqual(load_record(out2["recordText"], WBP)["textures"]["T_button_e62"], "h-slices/升级.png")

    def test_bold_without_bold_face_warns(self):
        self.fake.assets["/Game/F/MyFont"] = {"class": "Font", "props": {}}
        self.fake.font_faces["/Game/F/MyFont"] = ["Regular"]
        raw = layout_dict("ui-status")
        next(e for e in raw["elements"] if e["id"] == "e3")["text"]["bold"] = True
        out = self.run_import(request(layout=raw, font="/Game/F/MyFont"))
        font = self.fake.wbp(WBP).props["text_e3_Text"]["font"]
        self.assertEqual(font["fontObject"]["refPath"], "/Game/F/MyFont.MyFont")
        self.assertEqual(font["typefaceFontName"], "Regular")
        self.assertTrue(any("e3" in w and "Bold" in w for w in out["report"]["warnings"]))

    def test_orphan_textures(self):
        self.fake.assets["/Game/Tests/X/Textures/T_old_e99"] = {"class": "Texture2D", "props": {}}
        out = self.first()
        self.assertEqual(out["report"]["orphanTextures"], ["/Game/Tests/X/Textures/T_old_e99"])
        self.assertIn("/Game/Tests/X/Textures/T_old_e99", self.fake.assets)

    def assertBlocked(self, req, *fragments):
        with self.assertRaises(UiImportError) as cm:
            self.run_import(req)
        for f in fragments:
            self.assertIn(f, str(cm.exception))
        self.assertEqual(self.fake.write_calls(), [])

    def test_preflight_blocks_writes(self):
        self.fake.pie = True
        self.assertBlocked(request(), "PIE")
        self.fake.pie = False
        self.assertBlocked(request(record_text="{broken"), "导入记录")
        self.assertBlocked(request(font="/Game/F/Nope"), "/Game/F/Nope")

    def test_preflight_wbp_without_record(self):
        self.first()
        self.fake.calls.clear()
        self.assertBlocked(request(record_text=None), WBP, "记录")

    def test_preflight_record_without_wbp(self):
        out = self.first()
        del self.fake.wbps[WBP]
        self.fake.calls.clear()
        self.assertBlocked(request(record_text=out["recordText"]), WBP)

    def test_preflight_locked(self):
        out = self.first()
        self.fake.calls.clear()
        self.fake.locked.add(WBP)
        self.assertBlocked(request(record_text=out["recordText"]), WBP)
        self.fake.locked = {"/Game/Tests/X/Textures/T_panel_e1"}
        req = request(record_text=out["recordText"])
        req["hashes"]["slices/面板.png"] = "new"
        self.assertBlocked(req, "T_panel_e1")

    def test_preflight_bad_import_dir(self):
        req = request()
        req["importDir"] = "C:/Program Files/Git/Game/UI/X"
        self.assertBlocked(req, "MSYS_NO_PATHCONV")

    def test_compile_failure_not_saved(self):
        self.fake.compile_ok = False
        with self.assertRaises(UiImportError):
            self.first()
        self.assertEqual(self.fake.saved, [])


if __name__ == "__main__":
    unittest.main()
