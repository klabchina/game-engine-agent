import json
import unittest

from fixtures import layout_dict, layout_text
from seedance_ui.layout import parse_layout
from seedance_ui.planner import plan

WBP = "/Game/Tests/X/WBP_X"


def ops_of(p, kind):
    return [o for o in p["ops"] if o["op"] == kind]


def hashes_for(doc):
    return {e["image"]: "h-" + e["image"] for e in doc["elements"] if e["image"]}


class CreatePlanTests(unittest.TestCase):
    def test_create_plan_ui_status(self):
        doc = parse_layout(layout_text("ui-status"))
        p = plan(doc, WBP, None, None, hashes_for(doc))
        self.assertEqual(p["ops"][0], {"op": "ensure_root", "width": 2560, "height": 1440})
        containers = ops_of(p, "add_container")
        views = ops_of(p, "add_view")
        self.assertEqual(len(containers), 65)
        self.assertEqual(len(views), 65)
        by_name = {o["name"]: o for o in containers}
        self.assertEqual(by_name["button_e62"]["parent"], "panel_e1")
        self.assertEqual(by_name["panel_e1"]["parent"], "Design")
        self.assertEqual(by_name["button_e62"]["offsets"], (1367 - 237, 1157 - 127, 286, 105))
        # 每个容器建好后紧接着建它的显示控件（显示控件是容器的第一个子节点）
        names = [o.get("name") for o in p["ops"]]
        self.assertEqual(names.index("button_e62_Button"), names.index("button_e62") + 1)
        # 同级顺序 = 数组顺序
        under_e1 = [o["name"] for o in containers if o["parent"] == "panel_e1"]
        expected = ["%s_%s" % (e["type"], e["id"]) for e in doc["elements"] if e["parent"] == "e1"]
        self.assertEqual(under_e1, expected)
        view = next(v for v in views if v["name"] == "button_e62_Button")
        self.assertEqual((view["container"], view["cls"], view["texture"], view["element"]["id"]),
                         ("button_e62", "/Script/UMG.Button", "T_button_e62", "e62"))
        self.assertIsNone(next(v for v in views if v["name"] == "text_e63_Text")["texture"])
        self.assertEqual(next(v for v in views if v["name"] == "bar_e14_ProgressBar")["texture"], "T_bar_e11")

        tex = {t["name"]: t for t in p["textures"]}
        self.assertEqual(len(tex), len({e["image"] for e in doc["elements"] if e["image"] and not e["same_as"]}))
        self.assertEqual(tex["T_bar_e11"]["source_image"], "slices/属性条_1.png")
        self.assertEqual(tex["T_bar_e11"]["hash"], "h-slices/属性条_1.png")
        self.assertIn("e14", tex["T_bar_e11"]["elements"])

        rep = p["report"]
        self.assertEqual(len(rep["created"]), 65)
        self.assertIn("e62 升级", rep["created"])
        for k in ("updated", "rebuilt", "deactivated", "missing", "moved", "typeChanged", "orphanTextures", "warnings"):
            self.assertEqual(rep[k], [], k)
        self.assertEqual(rep["widgetBlueprint"], WBP)

        rec = p["record"]
        self.assertEqual([e["elementId"] for e in rec["elements"]], [e["id"] for e in doc["elements"]])
        self.assertEqual(rec["textures"], {t["name"]: t["hash"] for t in p["textures"]})

    def test_child_before_parent_still_nested(self):
        d = {"version": 1, "resolution": {"width": 100, "height": 100}, "elements": [
            {"id": "e2", "name": "子", "type": "icon", "image": "slices/a.png", "rect": {"x": 20, "y": 20, "w": 5, "h": 5}, "parent": "e1"},
            {"id": "e1", "name": "父", "type": "panel", "image": "slices/b.png", "rect": {"x": 10, "y": 10, "w": 50, "h": 50}, "parent": None},
        ]}
        doc = parse_layout(json.dumps(d))
        p = plan(doc, WBP, None, None, hashes_for(doc))
        names = [o.get("name") for o in p["ops"]]
        self.assertLess(names.index("panel_e1"), names.index("icon_e2"))
        c = {o["name"]: o for o in ops_of(p, "add_container")}
        self.assertEqual(c["icon_e2"]["parent"], "panel_e1")
        self.assertEqual(c["icon_e2"]["offsets"], (10, 10, 5, 5))

    def test_missing_element_has_no_view(self):
        d = layout_dict("ui-popup")
        d["elements"][1].update({"missing": True, "rect": None, "image": None})
        doc = parse_layout(json.dumps(d))
        eid = d["elements"][1]["id"]
        p = plan(doc, WBP, None, None, hashes_for(doc))
        cname = "%s_%s" % (d["elements"][1]["type"], eid)
        self.assertIn(cname, [o["name"] for o in ops_of(p, "add_container")])
        self.assertNotIn(cname, [o["container"] for o in ops_of(p, "add_view")])
        self.assertIn("%s %s" % (eid, d["elements"][1]["name"]), p["report"]["missing"])
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == eid)
        self.assertTrue(entry["missingAtCreate"])
        self.assertIsNone(entry["view"])


if __name__ == "__main__":
    unittest.main()
