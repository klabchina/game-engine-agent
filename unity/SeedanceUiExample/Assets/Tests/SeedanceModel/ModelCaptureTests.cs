using System.IO;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;
using static Seedance.ModelImport.Tests.ModelImportFixture;

namespace Seedance.ModelImport.Tests
{
    public class ModelCaptureTests
    {
        string png;

        [SetUp]
        public void SetUp() => png = Path.Combine(NewScratch(), "capture.png");

        [TearDown]
        public void TearDown()
        {
            CleanUp();
            Directory.Delete(Path.GetDirectoryName(png), true);
        }

        [Test]
        public void Capture_WritesPng_NotAllBackground()
        {
            SeedanceModelImporter.Run(Req());
            ModelCapture.Capture(Prefab, 640, 360, png);
            Assert.IsTrue(File.Exists(png));
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(png));
            Assert.AreEqual(640, t.width);
            Assert.AreEqual(360, t.height);
            var bg = (Color32)ModelCapture.BackgroundColor;
            var differs = 0;
            for (var x = 280; x < 360; x += 4)
            for (var y = 120; y < 240; y += 4)
            {
                var p = (Color32)t.GetPixel(x, y);
                if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 30) differs++;
            }
            Object.DestroyImmediate(t);
            Assert.Greater(differs, 50, "画面中间应该是角色，不是一片底色");
        }

        [Test]
        public void Run_ReportsViewerScene()
        {
            var report = SeedanceModelImporter.Run(Req());
            Assert.AreEqual(ViewerScene.ScenePath, report.scenePath);
            Assert.IsTrue(File.Exists(ViewerScene.ScenePath));
        }
    }
}
