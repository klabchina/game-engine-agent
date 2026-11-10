import copy
import json
import unittest

from fixtures import layout_dict
from plan_sim import tree_from_plan
from seedance_ui.errors import UiImportError
from seedance_ui.layout import parse_layout
from seedance_ui.planner import plan

WBP = "/Game/Tests/X/WBP_X"


def doc_of(raw):
    return parse_layout(json.dumps(raw))


def hashes_for(doc):
    return {e["image"]: "h-" + e["image"] for e in doc["elements"] if e["image"]}


def ops_of(p, kind):
    return [o for o in p["ops"] if o["op"] == kind]


def names_in(p, kind, key="name"):
    return [o[key] for o in ops_of(p, kind)]


class UpdatePlanTests(unittest.TestCase):
    def setUp(self):
        self.raw = layout_dict("ui-status")
        doc = doc_of(self.raw)
        self.first = plan(doc, WBP, None, None, hashes_for(doc))
        self.tree = tree_from_plan(self.first)
        self.rec = self.first["record"]

    def reimport(self, raw=None, tree=None, rec=None):
        doc = doc_of(raw or self.raw)
        return plan(doc, WBP, rec or self.rec, (tree or self.tree).as_dict(), hashes_for(doc))

    def el(self, raw, eid):
        return next(e for e in raw["elements"] if e["id"] == eid)

    def test_reimport_unchanged(self):
        p = self.reimport()
        self.assertEqual(len(p["report"]["updated"]), 65)
        for k in ("created", "rebuilt", "deactivated", "moved", "typeChanged"):
            self.assertEqual(p["report"][k], [], k)
        for k in ("add_container", "add_view", "move", "set_visibility", "ensure_root"):
            self.assertEqual(ops_of(p, k), [], k)
        self.assertEqual(len(ops_of(p, "set_offsets")), 65)
        self.assertIn("button_e62_Button", names_in(p, "set_texture", "view"))
        self.assertNotIn("text_e63_Text", names_in(p, "set_texture", "view"))
        self.assertEqual(p["record"]["elements"], self.rec["elements"])

    def test_find_by_view_when_container_renamed(self):
        self.tree.rename("button_e62", "UpgradeRoot")
        p = self.reimport()
        self.assertEqual(p["report"]["rebuilt"], [])
        self.assertIn("UpgradeRoot", names_in(p, "set_offsets"))
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e62")
        self.assertEqual(entry["container"], "UpgradeRoot")
        # 子元素（升级文字）还挂在改名后的容器下，不算被挪走
        self.assertNotIn("e63 升级文字", p["report"]["moved"])

    def test_find_by_container_when_view_renamed(self):
        self.tree.rename("button_e62_Button", "UpgradeButton")
        p = self.reimport()
        self.assertEqual(p["report"]["rebuilt"], [])
        self.assertIn("UpgradeButton", names_in(p, "set_texture", "view"))
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e62")
        self.assertEqual(entry["view"], "UpgradeButton")

    def test_both_renamed_rebuilds(self):
        self.tree.rename("button_e62", "UpgradeRoot")
        self.tree.rename("button_e62_Button", "UpgradeButton")
        p = self.reimport()
        self.assertIn("e62 升级", p["report"]["rebuilt"])
        self.assertIn("button_e62", names_in(p, "add_container"))
        self.assertIn("button_e62_Button", names_in(p, "add_view"))

    def test_moved_by_program(self):
        self.tree.move("bar_e11", "panel_e26", -1)
        p = self.reimport()
        self.assertIn("e11 属性条 1", p["report"]["moved"])
        self.assertIn("bar_e11", names_in(p, "set_size"))
        self.assertNotIn("bar_e11", names_in(p, "set_offsets"))
        self.assertNotIn("bar_e11", names_in(p, "move"))
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e11")
        self.assertEqual(entry["layoutParentId"], "e8")

    def test_upstream_reparent_follows(self):
        raw = copy.deepcopy(self.raw)
        self.el(raw, "e11")["parent"] = "e26"
        p = self.reimport(raw)
        mv = [o for o in ops_of(p, "move") if o["name"] == "bar_e11"]
        self.assertEqual(len(mv), 1)
        self.assertEqual(mv[0]["parent"], "panel_e26")
        self.assertIn("bar_e11", names_in(p, "set_offsets"))
        self.assertEqual(p["report"]["moved"], [])
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e11")
        self.assertEqual(entry["layoutParentId"], "e26")

    def test_parent_removed_child_follows_to_root(self):
        raw = copy.deepcopy(self.raw)
        raw["elements"] = [e for e in raw["elements"] if e["id"] != "e4"]
        self.el(raw, "e5")["parent"] = None
        p = self.reimport(raw)
        self.assertIn("e4 角色展示框", p["report"]["deactivated"])
        self.assertIn({"op": "set_visibility", "name": "frame_e4", "value": "Collapsed"}, p["ops"])
        mv = [o for o in ops_of(p, "move") if o["name"] == "text_e5"]
        self.assertEqual(mv[0]["parent"], "Design")
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e4")
        self.assertTrue(entry["deactivated"])

    def test_deactivated_restored(self):
        raw = copy.deepcopy(self.raw)
        raw["elements"] = [e for e in raw["elements"] if e["id"] not in ("e64", "e65")]
        p1 = self.reimport(raw)
        self.assertIn("e64 装备", p1["report"]["deactivated"])
        p2 = self.reimport(self.raw, rec=p1["record"])
        self.assertIn({"op": "set_visibility", "name": "button_e64", "value": "SelfHitTestInvisible"}, p2["ops"])
        self.assertTrue(any("e64" in w for w in p2["report"]["warnings"]))
        entry = next(e for e in p2["record"]["elements"] if e["elementId"] == "e64")
        self.assertFalse(entry.get("deactivated"))

    def test_type_changed_reported_only(self):
        raw = copy.deepcopy(self.raw)
        self.el(raw, "e8")["type"] = "frame"
        p = self.reimport(raw)
        self.assertIn("e8 属性栏 1", p["report"]["typeChanged"])
        self.assertEqual(ops_of(p, "add_view"), [])
        self.assertIn("panel_e8_Image", names_in(p, "set_texture", "view"))

    def test_missing_filled_by_record_flag(self):
        raw = copy.deepcopy(self.raw)
        self.el(raw, "e62").update({"missing": True, "rect": None, "image": None,
                                    "drawnRect": {"x": 1367, "y": 1157, "w": 286, "h": 105}})
        doc = doc_of(raw)
        first = plan(doc, WBP, None, None, hashes_for(doc))
        tree = tree_from_plan(first)
        p = self.reimport(self.raw, tree=tree, rec=first["record"])
        self.assertIn("button_e62_Button", names_in(p, "add_view"))
        entry = next(e for e in p["record"]["elements"] if e["elementId"] == "e62")
        self.assertEqual(entry["view"], "button_e62_Button")
        self.assertFalse(entry["missingAtCreate"])
        # 首次就有显示控件、程序删掉了：不补
        self.tree.remove("panel_e1_Image")
        p = self.reimport()
        self.assertNotIn("panel_e1_Image", names_in(p, "add_view"))
        self.assertNotIn("panel_e1_Image", names_in(p, "set_texture", "view"))

    def test_manual_widgets_untouched(self):
        self.tree.add("MyButton", "/Script/UMG.Button", "panel_e1")
        p = self.reimport()
        for o in p["ops"]:
            self.assertNotIn("MyButton", [o.get("name"), o.get("view"), o.get("after")])

    def test_texture_changes_size(self):
        raw = copy.deepcopy(self.raw)
        self.el(raw, "e2")["rect"].update({"w": 120, "h": 110})
        p = self.reimport(raw)
        st = next(o for o in ops_of(p, "set_texture") if o["view"] == "button_e2_Button")
        self.assertEqual((st["w"], st["h"], st["texture"]), (120, 110, "T_button_e2"))
        so = next(o for o in ops_of(p, "set_offsets") if o["name"] == "button_e2")
        self.assertEqual(so["offsets"], (2213 - 237, 134 - 127, 120, 110))

    def test_new_element_inserted_in_layout_order(self):
        raw = copy.deepcopy(self.raw)
        idx = raw["elements"].index(self.el(raw, "e62"))
        raw["elements"].insert(idx, {"id": "e99", "name": "新按钮", "type": "button", "image": "slices/升级.png",
                                     "rect": {"x": 1000, "y": 1157, "w": 286, "h": 105}, "parent": "e1"})
        p = self.reimport(raw)
        ac = next(o for o in ops_of(p, "add_container") if o["name"] == "button_e99")
        self.assertEqual(ac["parent"], "panel_e1")
        self.assertEqual(ac["after"], "panel_e44")
        self.assertIn("e99 新按钮", p["report"]["created"])

    def test_texture_hash_carried(self):
        p = self.reimport()
        t = next(t for t in p["textures"] if t["name"] == "T_button_e62")
        self.assertEqual(t["previous_hash"], "h-slices/升级.png")

    def test_missing_design_root_is_error(self):
        self.tree.rename("Design", "Whatever")
        with self.assertRaises(UiImportError):
            self.reimport()


if __name__ == "__main__":
    unittest.main()
