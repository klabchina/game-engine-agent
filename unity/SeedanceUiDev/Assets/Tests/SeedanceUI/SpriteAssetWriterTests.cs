using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class SpriteAssetWriterTests
    {
        UiLayoutDoc doc;

        [SetUp]
        public void SetUp() => doc = UiLayoutParser.Parse(TestPaths.CharPanelJson);

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        string SpritesDir => TestPaths.OutDir + "/Sprites";

        [Test]
        public void Write_NamesAndShares()
        {
            var r = SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            var files = Directory.GetFiles(SpritesDir, "*.png").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "bar_e11.png", "button_e2.png", "button_e6.png", "button_e62.png",
                "frame_e4.png", "panel_e1.png", "panel_e8.png", "tab_e9.png",
            }, files);
            Assert.IsNotNull(r.byElementId["e11"]);
            Assert.AreSame(r.byElementId["e11"], r.byElementId["e14"]);
            Assert.IsFalse(r.byElementId.ContainsKey("e3"), "text 元素不出图");
            Assert.IsEmpty(r.warnings);
        }

        [Test]
        public void Write_ImporterSettings()
        {
            SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            var imp = (TextureImporter)AssetImporter.GetAtPath(SpritesDir + "/panel_e1.png");
            Assert.AreEqual(TextureImporterType.Sprite, imp.textureType);
            Assert.AreEqual(SpriteImportMode.Single, imp.spriteImportMode);
            Assert.IsFalse(imp.mipmapEnabled);
            Assert.IsTrue(imp.alphaIsTransparency);
            Assert.AreEqual(TextureWrapMode.Clamp, imp.wrapMode);
            Assert.AreEqual(4096, imp.maxTextureSize);
            var small = (TextureImporter)AssetImporter.GetAtPath(SpritesDir + "/button_e2.png");
            Assert.AreEqual(128, small.maxTextureSize);
        }

        [Test]
        public void Write_Twice_KeepsGuid()
        {
            SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            var guid = AssetDatabase.AssetPathToGUID(SpritesDir + "/panel_e1.png");
            SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(SpritesDir + "/panel_e1.png"));
        }

        [Test]
        public void Write_ReportsOrphansWithoutDeleting()
        {
            Directory.CreateDirectory(SpritesDir);
            File.Copy(Path.Combine(TestPaths.CharPanelDir, "slices/关闭.png"), SpritesDir + "/panel_e99.png");
            AssetDatabase.Refresh();
            var r = SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            Assert.IsTrue(File.Exists(SpritesDir + "/panel_e99.png"));
            CollectionAssert.AreEqual(new[] { SpritesDir + "/panel_e99.png" }, r.orphanSprites);
        }
    }
}
