"""tools.py 经假编辑器的行为（也保证后续测试的故障注入可用）。"""

import json
import unittest

from fake_editor import FakeEditor
from seedance_ui import tools
from seedance_ui.errors import UiImportError

WBP = "/Game/Tests/X/WBP_X"


class ToolsTests(unittest.TestCase):
    def setUp(self):
        self.fake = FakeEditor().install(self)
        tools.create_wbp("/Game/Tests/X", "WBP_X")
        tools.add_widget(WBP, "/Script/UMG.CanvasPanel", "Root", None)
        tools.add_widget(WBP, "/Script/UMG.CanvasPanel", "A", "Root")
        tools.add_widget(WBP, "/Script/UMG.Image", "A_Image", "A", 0)
        tools.add_widget(WBP, "/Script/UMG.CanvasPanel", "B", "Root")

    def test_read_tree_after_add_and_move(self):
        tree = tools.read_tree(WBP)
        self.assertEqual(tree["Root"], {"class": "/Script/UMG.CanvasPanel", "parent": None, "index": 0})
        self.assertEqual(tree["A_Image"]["parent"], "A")
        self.assertEqual((tree["A"]["index"], tree["B"]["index"]), (0, 1))
        tools.move_widget(WBP, "B", "A", 1)
        tree = tools.read_tree(WBP)
        self.assertEqual((tree["B"]["parent"], tree["B"]["index"]), ("A", 1))

    def test_set_slot_uses_cached_slot(self):
        tools.set_slot(WBP, "A", {"layoutData": {"offsets": {"left": 1}}, "bAutoSize": False})
        self.assertEqual(self.fake.wbp(WBP).slot_props["A"]["layoutData"]["offsets"]["left"], 1)
        tools.move_widget(WBP, "A", "B", 0)
        tools.set_slot(WBP, "A", {"bAutoSize": True})
        self.assertTrue(self.fake.wbp(WBP).slot_props["A"]["bAutoSize"])

    def test_set_widget_merges(self):
        tools.set_widget(WBP, "A_Image", {"brush": {"imageSize": {"x": 1, "y": 2}}})
        tools.set_widget(WBP, "A_Image", {"brush": {"resourceObject": {"refPath": "/Game/T.T"}}})
        brush = self.fake.wbp(WBP).props["A_Image"]["brush"]
        self.assertEqual(brush["imageSize"], {"x": 1, "y": 2})

    def test_add_widget_name_collision_raises(self):
        with self.assertRaises(UiImportError) as cm:
            tools.add_widget(WBP, "/Script/UMG.CanvasPanel", "A", "Root")
        self.assertIn("A", str(cm.exception))

    def test_import_texture_sets_ui_settings(self):
        path = tools.import_texture("/Game/Tests/X/Textures", "T_a", "C:/x/a.png")
        self.assertEqual(path, "/Game/Tests/X/Textures/T_a")
        self.assertEqual(self.fake.assets[path]["props"], tools.TEXTURE_SETTINGS)
        self.assertEqual(tools.list_assets("/Game/Tests/X/Textures"), [path])

    def test_referencers_and_delete_nulls_refs(self):
        tools.import_texture("/Game/Tests/X/Textures", "T_a", "C:/x/a.png")
        tools.set_widget(WBP, "A_Image", {"brush": {"resourceObject": {"refPath": "/Game/Tests/X/Textures/T_a.T_a"}}})
        self.assertEqual(tools.referencers("/Game/Tests/X/Textures/T_a"), [WBP])
        tools.delete_asset("/Game/Tests/X/Textures/T_a")
        self.assertFalse(tools.asset_exists("/Game/Tests/X/Textures/T_a"))
        self.assertEqual(self.fake.wbp(WBP).props["A_Image"]["brush"]["resourceObject"], "None")

    def test_font_typefaces(self):
        self.assertIn("Bold", tools.font_typefaces(None))
        self.fake.assets["/Game/F/MyFont"] = {"class": "Font", "props": {}}
        self.fake.font_faces["/Game/F/MyFont"] = ["Regular"]
        self.assertEqual(tools.font_typefaces("/Game/F/MyFont"), ["Regular"])
        self.assertEqual(tools.asset_class("/Game/F/MyFont"), "Font")

    def test_compile_failure_raises(self):
        self.fake.compile_ok = False
        with self.assertRaises(UiImportError):
            tools.compile_wbp(WBP)

    def test_fail_on_injects_abort(self):
        self.fake.fail_on = {"save_assets"}
        with self.assertRaises(RuntimeError):
            tools.save_assets([WBP])

    def test_pie_and_can_edit(self):
        self.assertFalse(tools.is_pie_running())
        self.fake.pie = True
        self.assertTrue(tools.is_pie_running())
        self.fake.locked.add(WBP)
        self.assertFalse(tools.can_edit(WBP))


if __name__ == "__main__":
    unittest.main()
