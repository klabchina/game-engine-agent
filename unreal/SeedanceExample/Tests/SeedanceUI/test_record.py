import json
import unittest

from fixtures import layout_text
from seedance_ui.errors import UiImportError
from seedance_ui.layout import parse_layout
from seedance_ui import record

WBP = "/Game/Tests/X/WBP_X"


class RecordTests(unittest.TestCase):
    def setUp(self):
        self.doc = parse_layout(layout_text("ui-status"))

    def test_new_record(self):
        rec = record.new_record(WBP, self.doc)
        self.assertEqual(rec["formatVersion"], 1)
        self.assertEqual(rec["widgetBlueprint"], WBP)
        self.assertEqual((rec["designWidth"], rec["designHeight"], rec["layoutVersion"]), (2560, 1440, 1))
        self.assertEqual(rec["rootWidgets"], {"root": "Root", "scaleBox": "ScaleBox", "sizeBox": "SizeBox", "design": "Design"})
        self.assertEqual(rec["elements"], [])
        self.assertEqual(rec["textures"], {})

    def test_record_roundtrip(self):
        rec = record.new_record(WBP, self.doc)
        rec["elements"].append(record.element_entry(self.doc["by_id"]["e62"]))
        rec["textures"]["T_button_e62"] = "ab" * 32
        text = record.dump_record(rec)
        self.assertIn("升级", text)  # 中文不转义
        self.assertEqual(record.load_record(text, WBP), rec)
        e = rec["elements"][0]
        self.assertEqual(e, {"elementId": "e62", "container": "button_e62", "view": "button_e62_Button",
                             "sourceName": "升级", "type": "button", "layoutParentId": "e1", "missingAtCreate": False})

    def test_root_parent_marker(self):
        e = record.element_entry(self.doc["by_id"]["e1"])
        self.assertEqual(e["layoutParentId"], record.ROOT_PARENT)

    def test_dump_is_stable(self):
        rec = record.new_record(WBP, self.doc)
        self.assertEqual(record.dump_record(rec), record.dump_record(json.loads(record.dump_record(rec))))

    def test_record_rejects_unknown_format_version(self):
        rec = record.new_record(WBP, self.doc)
        rec["formatVersion"] = 2
        with self.assertRaises(UiImportError) as cm:
            record.load_record(record.dump_record(rec), WBP)
        self.assertIn("formatVersion", str(cm.exception))

    def test_record_rejects_other_wbp(self):
        rec = record.new_record("/Game/Other/WBP_Other", self.doc)
        with self.assertRaises(UiImportError) as cm:
            record.load_record(record.dump_record(rec), WBP)
        self.assertIn("/Game/Other/WBP_Other", str(cm.exception))

    def test_record_rejects_garbage(self):
        for bad in ("{oops", "[]", json.dumps({"formatVersion": 1, "widgetBlueprint": WBP}),
                    json.dumps({"formatVersion": 1, "widgetBlueprint": WBP, "elements": [{"elementId": ""}],
                                "textures": {}, "rootWidgets": {}})):
            with self.assertRaises(UiImportError):
                record.load_record(bad, WBP)


if __name__ == "__main__":
    unittest.main()
