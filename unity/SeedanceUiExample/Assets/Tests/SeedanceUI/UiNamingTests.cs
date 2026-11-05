using NUnit.Framework;
using Seedance.UiImport.Editor;

namespace Seedance.UiImport.Tests
{
    public class UiNamingTests
    {
        [Test]
        public void SpriteFileName_UsesMaster()
        {
            var doc = UiLayoutParser.Parse(TestPaths.CharPanelJson);
            Assert.AreEqual("panel_e1.png", UiNaming.SpriteFileName(doc, doc.Find("e1")));
            Assert.AreEqual("bar_e11.png", UiNaming.SpriteFileName(doc, doc.Find("e14")));
            Assert.AreEqual("panel_e8.png", UiNaming.SpriteFileName(doc, doc.Find("e26")));
            Assert.AreEqual("tab_e9.png", UiNaming.SpriteFileName(doc, doc.Find("e27")));
        }

        [Test]
        public void NodeName()
        {
            var doc = UiLayoutParser.Parse(TestPaths.CharPanelJson);
            Assert.AreEqual("text_e3", UiNaming.NodeName(doc.Find("e3")));
        }

        [TestCase("CharacterPanel", true)]
        [TestCase("ui_01-a", true)]
        [TestCase("角色", false)]
        [TestCase("a b", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("a/b", false)]
        public void IsValidPrefabName(string name, bool expected)
        {
            Assert.AreEqual(expected, UiNaming.IsValidPrefabName(name));
        }

        [Test]
        public void FontAssetName()
        {
            Assert.AreEqual("NotoSansCJKsc-Regular_SDF", UiNaming.FontAssetName("/x/NotoSansCJKsc-Regular.otf"));
            Assert.AreEqual("Font_SDF", UiNaming.FontAssetName("/x/思源 黑体.ttf"));
        }

        [Test]
        public void ToAscii_CollapsesAndTrims()
        {
            Assert.AreEqual("Noto_Sans", UiNaming.ToAscii(" Noto  Sans!"));
            Assert.AreEqual("a_b", UiNaming.ToAscii("a中文b"));
        }
    }
}
