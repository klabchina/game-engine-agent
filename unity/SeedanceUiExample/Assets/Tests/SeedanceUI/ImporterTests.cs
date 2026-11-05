using System.Linq;
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
            AssetDatabase.DeleteAsset(TestPaths.RecordOutDir);
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
        public void Run_FailsBeforeSave_WritesNoRecord()
        {
            var req = Req();
            req.fontFile = null;
            Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
            Assert.IsFalse(System.IO.File.Exists(UiRecordStore.RecordPathFor(ExpectedPrefab)));
        }

        [Test]
        public void Run_ExistingPrefabWithoutRecord_WritesNothing()
        {
            SeedanceUiImporter.Run(Req());
            AssetDatabase.DeleteAsset(UiRecordStore.RecordPathFor(ExpectedPrefab));
            var spritesDir = TestPaths.OutDir + "/CharacterPanel/Sprites";
            var stamps = System.IO.Directory.GetFiles(spritesDir).ToDictionary(f => f, System.IO.File.GetLastWriteTimeUtc);
            var prefabBytes = System.IO.File.ReadAllBytes(ExpectedPrefab);
            System.Threading.Thread.Sleep(1100); // 文件时间戳精度

            var ex = Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(Req(TestPaths.CharPanelDir)));
            StringAssert.Contains("找不到它的导入记录", ex.Message);
            foreach (var kv in stamps) Assert.AreEqual(kv.Value, System.IO.File.GetLastWriteTimeUtc(kv.Key), kv.Key);
            CollectionAssert.AreEqual(prefabBytes, System.IO.File.ReadAllBytes(ExpectedPrefab));
            Assert.IsFalse(System.IO.File.Exists(UiRecordStore.RecordPathFor(ExpectedPrefab)));
        }

        [Test]
        public void Run_FirstImport_BrokenRecordElsewhere_WritesNothing()
        {
            var broken = TestPaths.RecordOutDir + "/Other.json";
            System.IO.Directory.CreateDirectory(TestPaths.RecordOutDir);
            System.IO.File.WriteAllText(broken, "{x");
            var ex = Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(Req()));
            StringAssert.Contains(broken, ex.Message);
            Assert.IsFalse(System.IO.File.Exists(ExpectedPrefab), "坏记录要在写任何东西之前报出来");
            Assert.IsFalse(System.IO.Directory.Exists(TestPaths.OutDir + "/CharacterPanel/Sprites"));
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
