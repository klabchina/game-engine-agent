using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class ImporterTests
    {
        const string ImportDir = TestPaths.OutDir + "/CharacterPanel/";
        const string ExpectedPrefab = TestPaths.OutDir + "/CharacterPanel/CharacterPanel.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
            AssetDatabase.DeleteAsset("Assets/Product");
        }

        static ImportRequest Req(string source = null) => new ImportRequest
        {
            source = source ?? TestPaths.CharPanelZip,
            importDir = ImportDir,
            fontFile = TestPaths.RequireCachedFont(),
        };

        [Test]
        public void Run_Zip_EndToEnd()
        {
            var req = Req();
            req.prefabName = "CharacterPanel";
            var report = SeedanceUiImporter.Run(req);
            Assert.AreEqual(ExpectedPrefab, report.prefabPath);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(ExpectedPrefab));
            Assert.AreEqual(65, report.created.Count);
            Assert.AreEqual(ShowcaseScene.ScenePath, report.scenePath);
            Assert.IsEmpty(report.orphanSprites);
        }

        [Test]
        public void Run_Twice_SecondIsUpdate()
        {
            SeedanceUiImporter.Run(Req());
            var report = SeedanceUiImporter.Run(Req(TestPaths.CharPanelDir));
            Assert.IsEmpty(report.created);
            Assert.AreEqual(65, report.updated.Count);
        }

        [Test]
        public void Run_DefaultPrefabName()
        {
            Assert.AreEqual(ExpectedPrefab, SeedanceUiImporter.Run(Req()).prefabPath);
        }

        [Test]
        public void Run_Rejects_ImportDirOutsideAssets()
        {
            var req = Req();
            req.importDir = "Packages/x";
            Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
        }

        [Test]
        public void Run_Rejects_NonAsciiPrefabName()
        {
            var req = Req();
            req.prefabName = "角色";
            Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
            req.prefabName = null;
            req.importDir = TestPaths.OutDir + "/角色";
            Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
        }

        [Test]
        public void Run_Rejects_TextWithoutFont()
        {
            var req = Req();
            req.fontFile = null;
            Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
        }

        [Test]
        public void RequestJson_RoundTrip()
        {
            var req = JsonUtility.FromJson<ImportRequest>("{\"source\":\"/a/ui.zip\",\"importDir\":\"Assets/UI/A\",\"tmpFont\":\"Assets/F.asset\"}");
            Assert.AreEqual("/a/ui.zip", req.source);
            Assert.AreEqual("Assets/UI/A", req.importDir);
            Assert.AreEqual("Assets/F.asset", req.tmpFont);
        }
    }
}
