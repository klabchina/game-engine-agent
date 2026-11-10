import unittest

from fixtures import layout_text
from seedance_ui.layout import parse_layout
from seedance_ui import geometry


class GeometryTests(unittest.TestCase):
    def test_slot_offsets(self):
        doc = parse_layout(layout_text("ui-status"))
        e1, e2 = doc["by_id"]["e1"], doc["by_id"]["e2"]
        self.assertEqual(geometry.slot_offsets(e2, e1), (2213 - 237, 134 - 127, 105, 106))
        self.assertEqual(geometry.slot_offsets(e1, None), (237, 127, 2086, 1186))

    def test_font_size(self):
        self.assertEqual(geometry.font_size(48), (36.0, None))
        size, warn = geometry.font_size(None)
        self.assertEqual(size, 27.0)
        self.assertIsNotNone(warn)
        size, warn = geometry.font_size(0)
        self.assertEqual(size, 27.0)
        self.assertIsNotNone(warn)

    def test_parse_color(self):
        self.assertEqual(geometry.parse_color("#ffffff"), ((1.0, 1.0, 1.0, 1.0), None))
        (r, g, b, a), warn = geometry.parse_color("#80808080")
        self.assertIsNone(warn)
        self.assertAlmostEqual(a, 128 / 255, places=3)
        self.assertAlmostEqual(r, 0.2158, places=3)
        self.assertEqual((r, g), (g, b))
        (r, g, b, a), warn = geometry.parse_color("#000")
        self.assertIsNone(warn)
        self.assertEqual((r, g, b, a), (0.0, 0.0, 0.0, 1.0))
        rgba, warn = geometry.parse_color("red")
        self.assertEqual(rgba, (1.0, 1.0, 1.0, 1.0))
        self.assertIn("red", warn)
        rgba, warn = geometry.parse_color(None)
        self.assertEqual(rgba, (1.0, 1.0, 1.0, 1.0))
        self.assertIsNotNone(warn)

    def test_text_anchor(self):
        self.assertEqual(geometry.text_anchor("left"), (0.0, 0.5))
        self.assertEqual(geometry.text_anchor("center"), (0.5, 0.5))
        self.assertEqual(geometry.text_anchor("right"), (1.0, 0.5))
        self.assertEqual(geometry.text_anchor("weird"), (0.5, 0.5))

    def test_justification(self):
        self.assertEqual(geometry.justification("left"), "Left")
        self.assertEqual(geometry.justification("right"), "Right")
        self.assertEqual(geometry.justification(None), "Center")


if __name__ == "__main__":
    unittest.main()
