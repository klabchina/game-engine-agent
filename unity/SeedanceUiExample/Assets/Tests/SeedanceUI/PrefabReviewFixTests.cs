using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Seedance.UiImport.Tests.PrefabFixture;

namespace Seedance.UiImport.Tests
{
    /// <summary>最终审查提出的重导问题（C1、I1、I2、I3）</summary>
    public class PrefabReviewFixTests
    {
        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        static UiLayoutDoc DocWithMissingClose()
        {
            var doc = Doc();
            var e = doc.Find("e2");
            e.missing = true;
            e.rect = null;
            e.drawnRect = new UiRect { x = 2215, y = 136, w = 100, h = 100 };
            return doc;
        }

        [Test]
        public void Update_MissingThenProvided_AddsImageAndComponents()
        {
            Build(DocWithMissingClose());
            var (p, report) = Build();
            var n = Node(p, "button_e2");
            Assert.IsNotNull(n.GetComponent<Image>());
            Assert.IsNotNull(n.GetComponent<Image>().sprite);
            Assert.AreSame(n.GetComponent<Image>(), n.GetComponent<Button>().targetGraphic);
            Assert.AreEqual(new Vector2(1959, 9), Rt(p, "button_e2").anchoredPosition);
            Assert.AreEqual(new Vector2(105, 106), Rt(p, "button_e2").sizeDelta);
            CollectionAssert.IsEmpty(report.missing);
        }

        [Test]
        public void Update_IgnoresNestedSeedancePrefab()
        {
            Build();
            const string otherPath = TestPaths.OutDir + "/Other.prefab";
            AssetDatabase.CopyAsset(PrefabPath, otherPath);
            var other = AssetDatabase.LoadAssetAtPath<GameObject>(otherPath);
            EditPrefab(r => ((GameObject)PrefabUtility.InstantiatePrefab(other)).transform.SetParent(r.transform, false));

            var doc = Doc();
            doc.Find("e2").rect.x += 10;
            doc.elements.RemoveAll(e => e.id == "e64" || e.id == "e65");
            var (p, report) = Build(doc);

            Assert.AreEqual(new Vector2(1969, 9), ((RectTransform)p.transform.Find("panel_e1/button_e2")).anchoredPosition);
            var nested = p.transform.Find("Other");
            Assert.AreEqual(new Vector2(1959, 9), ((RectTransform)nested.Find("panel_e1/button_e2")).anchoredPosition, "嵌套的别的 Prefab 不能被改");
            Assert.IsTrue(nested.Find("panel_e1/button_e64").gameObject.activeSelf, "嵌套的别的 Prefab 不能被设 inactive");
            Assert.IsEmpty(report.warnings);
            CollectionAssert.AreEquivalent(new[] { "e64", "e65" }, report.deactivated.ConvertAll(l => l.Split(' ')[0]));
        }

        [Test]
        public void Update_UpstreamReparent_Follows()
        {
            Build();
            var doc = Doc();
            // 上游新加一个容器 e66 包住关闭按钮 e2，数组里排在 e2 之前
            doc.elements.Insert(1, new UiLayoutElement
            {
                id = "e66", name = "关闭底板", type = "panel", parent = "e1", missing = true,
                drawnRect = new UiRect { x = 2200, y = 120, w = 140, h = 140 },
            });
            doc.Find("e2").parent = "e66";
            var (p, report) = Build(doc);

            Assert.AreEqual("panel_e66", Node(p, "button_e2").transform.parent.name);
            Assert.AreEqual(new Vector2(13, -14), Rt(p, "button_e2").anchoredPosition);
            Assert.AreEqual(0, Node(p, "panel_e66").transform.GetSiblingIndex(), "新容器按数组顺序插入，不能盖在最上层");
            CollectionAssert.IsEmpty(report.moved);
            CollectionAssert.Contains(report.updated, "e2 关闭");
        }

        [Test]
        public void Update_MovedNode_StillUpdatesSize()
        {
            Build();
            EditPrefab(r =>
            {
                var custom = new GameObject("Custom", typeof(RectTransform));
                custom.transform.SetParent(r.transform, false);
                Node(r, "button_e2").transform.SetParent(custom.transform, false);
                ((RectTransform)Node(r, "button_e2").transform).anchoredPosition = new Vector2(5, 5);
            });
            var doc = Doc();
            doc.Find("e2").rect.w += 6;
            var (p, report) = Build(doc);
            Assert.AreEqual(new Vector2(5, 5), Rt(p, "button_e2").anchoredPosition);
            Assert.AreEqual(new Vector2(111, 106), Rt(p, "button_e2").sizeDelta);
            CollectionAssert.Contains(report.moved, "e2 关闭");
        }
    }
}
