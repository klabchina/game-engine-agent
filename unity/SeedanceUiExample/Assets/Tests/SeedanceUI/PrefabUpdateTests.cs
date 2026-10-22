using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Seedance.UiImport.Tests.PrefabFixture;

namespace Seedance.UiImport.Tests
{
    public class PrefabUpdateTests
    {
        [SetUp]
        public void SetUp() => Build();

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        static GameObject Load() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        [Test]
        public void Update_KeepsGuid()
        {
            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Build();
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(PrefabPath));
        }

        [Test]
        public void Update_MovesAndResprites()
        {
            var doc = Doc();
            doc.Find("e2").rect.x += 10;
            var (p, report) = Build(doc);
            Assert.AreEqual(new Vector2(1969, 9), Rt(p, "button_e2").anchoredPosition);
            CollectionAssert.Contains(report.updated, "e2 " + doc.Find("e2").name);
            Assert.IsEmpty(report.created);
            Assert.AreEqual(1, Load().GetComponentsInChildren<SeedanceUiElement>(true).Count(m => m.elementId == "e2"));
        }

        [Test]
        public void Update_RestoresSpriteFromLayout()
        {
            EditPrefab(r => Node(r, "button_e2").GetComponent<Image>().sprite = null);
            var (p, _) = Build();
            Assert.IsNotNull(Node(p, "button_e2").GetComponent<Image>().sprite);
        }

        [Test]
        public void Update_KeepsManualTextAndComponents()
        {
            EditPrefab(r =>
            {
                Text(r, "text_e3").text = "loc_key";
                Node(r, "panel_e1").AddComponent<AudioSource>();
                new GameObject("Custom", typeof(RectTransform)).transform.SetParent(r.transform, false);
            });
            var (p, _) = Build();
            Assert.AreEqual("loc_key", Text(p, "text_e3").text);
            Assert.IsNotNull(Node(p, "panel_e1").GetComponent<AudioSource>());
            Assert.IsNotNull(Node(p, "Custom"));
        }

        [Test]
        public void Update_KeepsManualSiblingOrderAndActive()
        {
            EditPrefab(r =>
            {
                Node(r, "button_e64").transform.SetAsFirstSibling();
                Node(r, "frame_e4").SetActive(false);
            });
            var (p, _) = Build();
            Assert.AreEqual(0, Node(p, "button_e64").transform.GetSiblingIndex());
            Assert.IsFalse(Node(p, "frame_e4").activeSelf);
        }

        [Test]
        public void Update_MovedNode_KeepsParentAndReports()
        {
            EditPrefab(r =>
            {
                var custom = new GameObject("Custom", typeof(RectTransform));
                custom.transform.SetParent(r.transform, false);
                Node(r, "button_e2").transform.SetParent(custom.transform, false);
                ((RectTransform)Node(r, "button_e2").transform).anchoredPosition = new Vector2(5, 5);
            });
            var doc = Doc();
            doc.Find("e2").rect.x += 10;
            var (p, report) = Build(doc);
            Assert.AreEqual("Custom", Node(p, "button_e2").transform.parent.name);
            Assert.AreEqual(new Vector2(5, 5), Rt(p, "button_e2").anchoredPosition);
            CollectionAssert.Contains(report.moved, "e2 " + doc.Find("e2").name);
            CollectionAssert.DoesNotContain(report.updated, "e2 " + doc.Find("e2").name);
        }

        [Test]
        public void Update_RemovedElement_Deactivated()
        {
            var doc = Doc();
            doc.elements.RemoveAll(e => e.id == "e64" || e.id == "e65");
            var (p, report) = Build(doc);
            Assert.IsFalse(Node(p, "button_e64").activeSelf);
            CollectionAssert.AreEquivalent(new[] { "e64", "e65" }, report.deactivated.Select(l => l.Split(' ')[0]));
        }

        [Test]
        public void Update_NewElement_Created()
        {
            var doc = Doc();
            doc.elements.Add(new UiLayoutElement
            {
                id = "e66", name = "新图标", type = "icon", parent = "e1", missing = true,
                drawnRect = new UiRect { x = 300, y = 200, w = 50, h = 50 },
            });
            var (p, report) = Build(doc);
            Assert.AreEqual("panel_e1", Node(p, "icon_e66").transform.parent.name);
            Assert.AreEqual(new Vector2(46, -57), Rt(p, "icon_e66").anchoredPosition);
            CollectionAssert.AreEqual(new[] { "e66 新图标" }, report.created);
        }

        [Test]
        public void Update_TypeChanged_Reported()
        {
            var doc = Doc();
            doc.Find("e8").type = "button";
            var (p, report) = Build(doc);
            Assert.IsNull(Node(p, "panel_e8").GetComponent<Button>());
            Assert.AreEqual("panel", Node(p, "panel_e8").GetComponent<SeedanceUiElement>().type);
            CollectionAssert.Contains(report.typeChanged, "e8 " + doc.Find("e8").name);
        }
    }
}
