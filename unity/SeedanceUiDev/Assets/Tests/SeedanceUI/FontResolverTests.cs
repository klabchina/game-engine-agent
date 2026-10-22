using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class FontResolverTests
    {
        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        [Test]
        public void Resolve_ExistingAsset()
        {
            var created = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir);
            var path = AssetDatabase.GetAssetPath(created);
            Assert.AreSame(created, FontResolver.Resolve(path, null, TestPaths.OutDir));
        }

        [Test]
        public void Resolve_BadAssetPath_Throws()
        {
            Assert.Throws<UiImportException>(() => FontResolver.Resolve("Assets/nope.asset", null, TestPaths.OutDir));
        }

        [Test]
        public void Resolve_NoFont_Throws()
        {
            Assert.Throws<UiImportException>(() => FontResolver.Resolve(null, null, TestPaths.OutDir));
            Assert.Throws<UiImportException>(() => FontResolver.Resolve(null, "/nonexistent/a.otf", TestPaths.OutDir));
        }

        [Test]
        public void Resolve_FromFontFile_CreatesPersistentAsset()
        {
            var fa = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir);
            // 工程里别处已经生成过同一字体时会共用那份（见 Resolve_ReusesFontGeneratedByAnotherImport），所以只认文件名
            var path = AssetDatabase.GetAssetPath(fa);
            StringAssert.EndsWith("/Fonts/NotoSansCJKsc-Regular_SDF.asset", path);
            Assert.IsTrue(File.Exists(Path.GetDirectoryName(path) + "/NotoSansCJKsc-Regular.otf"));
            var subs = AssetDatabase.LoadAllAssetsAtPath(path);
            Assert.IsTrue(subs.OfType<Texture2D>().Any(), "atlas 贴图要挂成子资源");
            Assert.IsTrue(subs.OfType<Material>().Any(), "材质要挂成子资源");
            Assert.AreEqual(AtlasPopulationMode.Dynamic, fa.atlasPopulationMode);
            Assert.IsTrue(fa.HasCharacters("角色属性", out _, false, true));
        }

        [Test]
        public void Resolve_Twice_Reuses()
        {
            var a = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir);
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a));
            var b = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(b)));
        }

        [Test]
        public void Resolve_ReusesFontGeneratedByAnotherImport()
        {
            var a = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir + "/A");
            var b = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir + "/B");
            Assert.AreEqual(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b));
            Assert.IsFalse(Directory.Exists(TestPaths.OutDir + "/B/Fonts"), "同一个字体不能每个导入目录各拷一份");
        }

        [Test]
        public void TmpEssentialsReady_InDevProject()
        {
            // 开发工程已经导入过（提交在 Assets/TextMesh Pro/）；没导入的路径在真实验收（Task 11）里走
            Assert.IsTrue(FontResolver.TmpEssentialsReady());
        }
    }
}
