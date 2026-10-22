using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using static Seedance.UiImport.Tests.PrefabFixture;

namespace Seedance.UiImport.Tests
{
    public class RobustnessTests
    {
        [SetUp]
        public void SetUp() => SessionState.EraseString(FontResolver.TmpImportStartedKey);

        [TearDown]
        public void TearDown()
        {
            SessionState.EraseString(FontResolver.TmpImportStartedKey);
            SeedanceUiImporter.IsPlaying = SeedanceUiImporter.DefaultIsPlaying;
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
        }

        [Test]
        public void Prepare_Ready_DoesNotImport()
        {
            var starts = 0;
            Assert.AreEqual("ready", FontResolver.PrepareTmpEssentials(() => true, () => starts++));
            Assert.AreEqual(0, starts);
        }

        [Test]
        public void Prepare_CalledTwiceWhileImporting_StartsImportOnce()
        {
            var starts = 0;
            Assert.AreEqual("importing", FontResolver.PrepareTmpEssentials(() => false, () => starts++));
            Assert.AreEqual("importing", FontResolver.PrepareTmpEssentials(() => false, () => starts++));
            Assert.AreEqual(1, starts);
        }

        [Test]
        public void Prepare_StaleImport_StartsAgain()
        {
            var starts = 0;
            FontResolver.PrepareTmpEssentials(() => false, () => starts++);
            // 上次触发已过去很久还没就绪（导入失败或被取消），允许重新触发
            SessionState.SetString(FontResolver.TmpImportStartedKey, System.DateTime.UtcNow.AddMinutes(-10).Ticks.ToString());
            FontResolver.PrepareTmpEssentials(() => false, () => starts++);
            Assert.AreEqual(2, starts);
        }

        [Test]
        public void Run_InPlayMode_RejectsBeforeWritingAnything()
        {
            SeedanceUiImporter.IsPlaying = () => true;
            var req = new ImportRequest { source = TestPaths.CharPanelZip, importDir = TestPaths.OutDir + "/X", fontFile = TestPaths.CachedFont };
            var ex = Assert.Throws<UiImportException>(() => SeedanceUiImporter.Run(req));
            StringAssert.Contains("Play", ex.Message);
            Assert.IsFalse(Directory.Exists(TestPaths.OutDir));
        }

        [Test]
        public void Build_TextWithoutSize_FallsBackToDefault()
        {
            var doc = Doc();
            doc.Find("e3").text = new UiText { content = "x", color = "#ffffff", align = "center" };
            doc.Find("e5").text = null;
            var (p, report) = Build(doc);
            Assert.AreEqual(36f, Text(p, "text_e3").fontSize);
            Assert.AreEqual(36f, Text(p, "text_e5").fontSize);
            Assert.AreEqual("", Text(p, "text_e5").text);
            Assert.AreEqual(2, report.warnings.FindAll(w => w.Contains("字号")).Count);
        }

        [Test]
        public void CliAssembly_CompiledWhenPipelineInstalled()
        {
            // Cli 程序集靠 versionDefines（com.unity.pipeline → SEEDANCE_HAS_PIPELINE）决定编不编；开发工程装了 pipeline，必须编进来
            var t = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Seedance.UiImport.Editor.SeedanceUiImportCommand"))
                .FirstOrDefault(x => x != null);
            Assert.IsNotNull(t);
        }
    }
}
