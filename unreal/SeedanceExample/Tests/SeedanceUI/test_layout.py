import json
import unittest

from fixtures import layout_dict, layout_text
from seedance_ui.errors import UiImportError
from seedance_ui.layout import parse_layout


def minimal(**over):
    doc = {
        "version": 1,
        "resolution": {"width": 1920, "height": 1080},
        "elements": [
            {"id": "e1", "name": "面板", "type": "panel", "image": "slices/面板.png",
             "rect": {"x": 10, "y": 20, "w": 300, "h": 200}, "drawnRect": {"x": 12, "y": 22, "w": 296, "h": 196},
             "parent": None, "order": 0, "sameAs": None},
        ],
    }
    doc.update(over)
    return doc


class LayoutTests(unittest.TestCase):
    def test_parses_ui_status(self):
        doc = parse_layout(layout_text("ui-status"))
        self.assertEqual((doc["width"], doc["height"]), (2560, 1440))
        self.assertEqual(len(doc["elements"]), 65)
        by_id = doc["by_id"]
        self.assertEqual(by_id["e14"]["same_as"], "e11")
        self.assertEqual(by_id["e5"]["parent"], "e4")
        self.assertIsNone(by_id["e1"]["parent"])
        self.assertEqual(by_id["e3"]["text"]["content"], "角色属性")
        self.assertIsNone(by_id["e1"]["text"])
        self.assertEqual(by_id["e2"]["rect"], (2213, 134, 105, 106))
        self.assertEqual(by_id["e2"]["image"], "slices/关闭.png")
        self.assertFalse(by_id["e2"]["missing"])

    def test_parses_ui_popup(self):
        doc = parse_layout(layout_text("ui-popup"))
        self.assertEqual(len(doc["elements"]), len(layout_dict("ui-popup")["elements"]))

    def test_text_fields(self):
        raw = layout_dict("ui-status")
        src = next(e for e in raw["elements"] if e["id"] == "e3")["text"]
        t = parse_layout(layout_text("ui-status"))["by_id"]["e3"]["text"]
        self.assertEqual(t, {"content": src["content"], "font_size": src["fontSize"], "color": src["color"],
                             "align": src["align"], "bold": src["bold"]})

    def test_missing_uses_drawn_rect(self):
        d = minimal()
        d["elements"][0].update({"missing": True, "rect": None})
        el = parse_layout(json.dumps(d))["elements"][0]
        self.assertTrue(el["missing"])
        self.assertEqual(el["rect"], (12, 22, 296, 196))

    def assertRejected(self, doc_or_text, *fragments):
        text = doc_or_text if isinstance(doc_or_text, str) else json.dumps(doc_or_text)
        with self.assertRaises(UiImportError) as cm:
            parse_layout(text)
        msg = str(cm.exception)
        self.assertRegex(msg, "[一-鿿]")  # 中文消息
        for f in fragments:
            self.assertIn(f, msg)

    def test_rejects_bad_input(self):
        self.assertRejected("{not json")
        self.assertRejected(minimal(version=2), "2")
        self.assertRejected(minimal(resolution=None), "resolution")
        d = minimal()
        d["elements"].append(dict(d["elements"][0]))
        self.assertRejected(d, "e1")
        d = minimal()
        d["elements"][0]["parent"] = "e9"
        self.assertRejected(d, "e1", "e9")
        d = minimal()
        d["elements"][0]["sameAs"] = "e8"
        self.assertRejected(d, "e1", "e8")
        d = minimal()
        d["elements"][0].update({"rect": None, "drawnRect": None})
        self.assertRejected(d, "e1")
        d = minimal()
        d["elements"][0].pop("id")
        self.assertRejected(d)
        d = minimal()
        d["elements"][0]["parent"] = "e1"
        self.assertRejected(d, "e1")


if __name__ == "__main__":
    unittest.main()
