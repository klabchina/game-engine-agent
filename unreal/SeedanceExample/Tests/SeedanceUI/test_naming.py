import json
import unittest

from fixtures import layout_text
from seedance_ui.errors import UiImportError
from seedance_ui.layout import parse_layout
from seedance_ui import naming


def status():
    return parse_layout(layout_text("ui-status"))


class NamingTests(unittest.TestCase):
    def test_texture_name_uses_same_as_master(self):
        doc = status()
        self.assertEqual(naming.texture_name(doc, doc["by_id"]["e14"]), "T_bar_e11")
        self.assertEqual(naming.texture_name(doc, doc["by_id"]["e11"]), "T_bar_e11")
        self.assertEqual(naming.texture_name(doc, doc["by_id"]["e1"]), "T_panel_e1")

    def test_names(self):
        doc = status()
        e62, e63 = doc["by_id"]["e62"], doc["by_id"]["e63"]
        self.assertEqual(naming.container_name(e62), "button_e62")
        self.assertEqual(naming.view_name(e62), "button_e62_Button")
        self.assertEqual(naming.view_name(e63), "text_e63_Text")
        self.assertEqual(naming.view_name(doc["by_id"]["e9"]), "tab_e9_CheckBox")
        self.assertEqual(naming.view_name(doc["by_id"]["e11"]), "bar_e11_ProgressBar")
        self.assertEqual(naming.view_name(doc["by_id"]["e4"]), "frame_e4_Image")
        missing = dict(e62, missing=True)
        self.assertIsNone(naming.view_name(missing))

    def test_view_class(self):
        self.assertEqual(naming.view_class("button"), "/Script/UMG.Button")
        self.assertEqual(naming.view_class("tab"), "/Script/UMG.CheckBox")
        self.assertEqual(naming.view_class("bar"), "/Script/UMG.ProgressBar")
        self.assertEqual(naming.view_class("text"), "/Script/UMG.TextBlock")
        for t in ("panel", "slot", "icon", "frame", "image", "unknown"):
            self.assertEqual(naming.view_class(t), "/Script/UMG.Image")

    def test_wbp_path(self):
        self.assertEqual(naming.wbp_path("/Game/UI/X", None), "/Game/UI/X/WBP_X")
        self.assertEqual(naming.wbp_path("/Game/UI/X/", "Popup"), "/Game/UI/X/WBP_Popup")
        with self.assertRaises(UiImportError):
            naming.wbp_path("/Game/UI/角色", None)
        with self.assertRaises(UiImportError):
            naming.wbp_path("/Game/UI/X", "坏名字")
        with self.assertRaises(UiImportError):
            naming.wbp_path("/Content/X", None)
        with self.assertRaises(UiImportError):
            naming.wbp_path("C:/Program Files/Git/Game/UI/X", None)

    def test_record_file_path(self):
        self.assertEqual(naming.record_file_path("/Game/UI/X/WBP_X"), "/Game/SeedanceKlab/UI/UI/X/WBP_X.json")

    def test_texture_folder(self):
        self.assertEqual(naming.texture_folder("/Game/UI/X"), "/Game/UI/X/Textures")


if __name__ == "__main__":
    unittest.main()
