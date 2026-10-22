using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Seedance.UiImport.Tests.PrefabFixture;

namespace Seedance.UiImport.Tests
{
    public class PrefabBuildTests
    {
        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        [Test]
        public void Build_Hierarchy()
        {
            var (p, _) = Build();
            Assert.AreEqual("CharacterPanel", p.name);
            var rt = (RectTransform)p.transform;
            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.IsNull(p.GetComponent<Canvas>());
            Assert.IsNull(p.GetComponent<CanvasScaler>());
            Assert.IsNotNull(p.GetComponent<SeedanceUiRoot>());
            Assert.AreEqual(2560, p.GetComponent<SeedanceUiRoot>().designWidth);

            Assert.AreEqual("panel_e1", Node(p, "button_e2").transform.parent.name);
            Assert.AreEqual("bar_e11", Node(p, "text_e13").transform.parent.name);
            var children = Node(p, "panel_e1").transform.Cast<Transform>().Select(t => t.name).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "button_e2", "text_e3", "frame_e4", "button_e6", "panel_e8", "panel_e26", "panel_e44", "button_e62", "button_e64",
            }, children);
        }

        [Test]
        public void Build_Positions()
        {
            var (p, _) = Build();
            // 根下的元素相对 Prefab 根居中
            Assert.AreEqual(new Vector2(2, -1), Rt(p, "panel_e1").anchoredPosition);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), Rt(p, "panel_e1").anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), Rt(p, "panel_e1").anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), Rt(p, "panel_e1").pivot);
            Assert.AreEqual(new Vector2(1959, 9), Rt(p, "button_e2").anchoredPosition);
            Assert.AreEqual(new Vector2(105, 106), Rt(p, "button_e2").sizeDelta);
            Assert.AreEqual(new Vector2(776, -27), Rt(p, "text_e3").anchoredPosition);
            Assert.AreEqual(new Vector2(0, 1), Rt(p, "text_e3").pivot);
        }

        [Test]
        public void Build_Components()
        {
            var (p, _) = Build();
            var closeImg = Node(p, "button_e2").GetComponent<Image>();
            Assert.AreSame(closeImg, Node(p, "button_e2").GetComponent<Button>().targetGraphic);
            Assert.IsTrue(closeImg.raycastTarget);
            Assert.AreSame(Node(p, "tab_e9").GetComponent<Image>(), Node(p, "tab_e9").GetComponent<Toggle>().targetGraphic);
            var bar = Node(p, "bar_e11").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Filled, bar.type);
            Assert.AreEqual(Image.FillMethod.Horizontal, bar.fillMethod);
            Assert.AreEqual(1f, bar.fillAmount);
            Assert.IsFalse(Node(p, "panel_e1").GetComponent<Image>().raycastTarget);
            Assert.IsNotNull(bar.sprite);
            Assert.AreSame(bar.sprite, Node(p, "bar_e14").GetComponent<Image>().sprite);
            Assert.IsNull(Node(p, "panel_e1").GetComponent<Button>());
        }

        [Test]
        public void Build_Text()
        {
            var (p, _) = Build();
            var t = Text(p, "text_e3");
            Assert.AreEqual("角色属性", t.text);
            Assert.AreEqual(64f, t.fontSize);
            Assert.AreEqual((Color)new Color32(0xff, 0xe9, 0xb0, 0xff), t.color);
            Assert.AreEqual(TextAlignmentOptions.Center, t.alignment);
            Assert.AreEqual(FontStyles.Bold, t.fontStyle);
            Assert.AreEqual(TextWrappingModes.NoWrap, t.textWrappingMode);
            Assert.IsFalse(t.raycastTarget);
            Assert.IsNotNull(t.font);
            Assert.AreEqual(TextAlignmentOptions.Left, Text(p, "text_e12").alignment);
            Assert.AreEqual(TextAlignmentOptions.Right, Text(p, "text_e13").alignment);
        }

        [Test]
        public void Build_Markers()
        {
            var (p, report) = Build();
            var markers = p.GetComponentsInChildren<SeedanceUiElement>(true);
            Assert.AreEqual(65, markers.Length);
            CollectionAssert.AreEquivalent(Doc().elements.Select(e => e.id), markers.Select(m => m.elementId));
            var m3 = Node(p, "text_e3").GetComponent<SeedanceUiElement>();
            Assert.AreEqual("标题", m3.sourceName);
            Assert.AreEqual("text", m3.type);
            Assert.AreEqual(65, report.created.Count);
            Assert.AreEqual("e3 标题", report.created[2]);
        }

        [Test]
        public void Build_MissingPlaceholder()
        {
            var doc = Doc();
            var e = doc.Find("e2");
            e.missing = true;
            e.rect = null;
            e.drawnRect = new UiRect { x = 2215, y = 136, w = 100, h = 100 };
            var (p, report) = Build(doc);
            var n = Node(p, "button_e2");
            Assert.IsNull(n.GetComponent<Image>());
            Assert.IsNull(n.GetComponent<Button>());
            Assert.IsNotNull(n.GetComponent<SeedanceUiElement>());
            Assert.AreEqual(new Vector2(1961, 7), ((RectTransform)n.transform).anchoredPosition);
            CollectionAssert.Contains(report.missing, "e2 " + e.name);
        }
    }
}
