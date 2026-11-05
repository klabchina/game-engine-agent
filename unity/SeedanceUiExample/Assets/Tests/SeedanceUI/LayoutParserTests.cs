using NUnit.Framework;
using Seedance.UiImport.Editor;

namespace Seedance.UiImport.Tests
{
    public class LayoutParserTests
    {
        [Test]
        public void Parse_RealLayout()
        {
            var doc = UiLayoutParser.Parse(TestPaths.CharPanelJson);
            Assert.AreEqual(1, doc.version);
            Assert.AreEqual(2560, doc.resolution.width);
            Assert.AreEqual(1440, doc.resolution.height);
            Assert.AreEqual(65, doc.elements.Count);

            var e1 = doc.Find("e1");
            Assert.AreEqual("panel", e1.type);
            Assert.AreEqual(254, e1.rect.x);
            Assert.AreEqual(143, e1.rect.y);
            Assert.AreEqual(2056, e1.rect.w);
            Assert.AreEqual(1156, e1.rect.h);
            Assert.IsNull(e1.parent);
            Assert.IsNull(e1.sameAs);
            Assert.IsNull(e1.text);

            var e14 = doc.Find("e14");
            Assert.AreEqual("e11", e14.sameAs);
            Assert.AreEqual("slices/属性条_1.png", e14.image);
            Assert.AreEqual("e8", e14.parent);

            var e3 = doc.Find("e3");
            Assert.IsTrue(e3.IsText);
            Assert.AreEqual("角色属性", e3.text.content);
            Assert.AreEqual(64f, e3.text.fontSize);
            Assert.AreEqual("#ffe9b0", e3.text.color);
            Assert.AreEqual("center", e3.text.align);
            Assert.IsTrue(e3.text.bold);
            Assert.IsNull(e3.image);
            Assert.AreEqual(1030, e3.EffectiveRect.x);
        }

        [Test]
        public void Parse_MissingElement()
        {
            var json = Doc("{\"id\":\"e1\",\"name\":\"a\",\"type\":\"icon\",\"missing\":true,\"rect\":null," +
                           "\"drawnRect\":{\"x\":1,\"y\":2,\"w\":3,\"h\":4},\"parent\":null,\"order\":0,\"sameAs\":null}");
            var e = UiLayoutParser.Parse(json).Find("e1");
            Assert.IsTrue(e.missing);
            Assert.IsNull(e.rect);
            Assert.AreEqual(1, e.EffectiveRect.x);
            Assert.AreEqual(4, e.EffectiveRect.h);
        }

        static string El(string id, string parent = null) =>
            "{\"id\":\"" + id + "\",\"name\":\"" + id + "\",\"type\":\"icon\",\"image\":\"slices/a.png\"," +
            "\"rect\":{\"x\":0,\"y\":0,\"w\":1,\"h\":1},\"parent\":" + (parent == null ? "null" : "\"" + parent + "\"") + ",\"order\":0}";

        static string Doc(string elements, int version = 1, bool resolution = true) =>
            "{\"version\":" + version + (resolution ? ",\"resolution\":{\"width\":2560,\"height\":1440}" : "") +
            ",\"background\":null,\"elements\":[" + elements + "]}";

        [Test]
        public void Parse_Rejects_WrongVersion()
        {
            var ex = Assert.Throws<UiImportException>(() => UiLayoutParser.Parse(Doc(El("e1"), version: 2)));
            StringAssert.Contains("version", ex.Message);
        }

        [Test]
        public void Parse_Rejects_MissingResolution()
        {
            var ex = Assert.Throws<UiImportException>(() => UiLayoutParser.Parse(Doc(El("e1"), resolution: false)));
            StringAssert.Contains("resolution", ex.Message);
        }

        [Test]
        public void Parse_Rejects_DuplicateId()
        {
            var ex = Assert.Throws<UiImportException>(() => UiLayoutParser.Parse(Doc(El("e1") + "," + El("e1"))));
            StringAssert.Contains("e1", ex.Message);
        }

        [Test]
        public void Parse_Rejects_UnknownParent()
        {
            var ex = Assert.Throws<UiImportException>(() => UiLayoutParser.Parse(Doc(El("e1", parent: "e9"))));
            StringAssert.Contains("e9", ex.Message);
        }
    }
}
