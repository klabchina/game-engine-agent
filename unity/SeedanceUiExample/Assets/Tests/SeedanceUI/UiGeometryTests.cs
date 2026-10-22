using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class UiGeometryTests
    {
        static UiRect R(int x, int y, int w, int h) => new UiRect { x = x, y = y, w = w, h = h };

        [Test]
        public void Place_RootChild()
        {
            var p = UiGeometry.Place(R(254, 143, 2056, 1156), null);
            Assert.AreEqual(new Vector2(254, -143), p.anchoredPosition);
            Assert.AreEqual(new Vector2(2056, 1156), p.size);
        }

        [Test]
        public void Place_Nested()
        {
            var p = UiGeometry.Place(R(2213, 134, 105, 106), R(254, 143, 2056, 1156));
            Assert.AreEqual(new Vector2(1959, 9), p.anchoredPosition);
            Assert.AreEqual(new Vector2(105, 106), p.size);
        }

        [Test]
        public void Match_LandscapeIsZero() => Assert.AreEqual(0f, UiGeometry.MatchFor(2560, 1440));

        [Test]
        public void Match_PortraitIsOne() => Assert.AreEqual(1f, UiGeometry.MatchFor(1440, 2560));

        [Test]
        public void Apply_SetsTopLeftAnchors()
        {
            var go = new GameObject("n", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            UiGeometry.Apply(rt, UiGeometry.Place(R(10, 20, 30, 40), null));
            Assert.AreEqual(new Vector2(0, 1), rt.anchorMin);
            Assert.AreEqual(new Vector2(0, 1), rt.anchorMax);
            Assert.AreEqual(new Vector2(0, 1), rt.pivot);
            Assert.AreEqual(new Vector2(10, -20), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(30, 40), rt.sizeDelta);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Stretch_FillsParent()
        {
            var go = new GameObject("n", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            UiGeometry.Stretch(rt);
            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.AreEqual(Vector2.zero, rt.offsetMin);
            Assert.AreEqual(Vector2.zero, rt.offsetMax);
            Object.DestroyImmediate(go);
        }
    }
}
