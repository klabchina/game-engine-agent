using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seedance.UiImport.Tests
{
    public class PrefabCaptureTests
    {
        string png;

        [SetUp]
        public void SetUp() => png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"seedance-capture-{System.Guid.NewGuid():N}.png");

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
            if (System.IO.File.Exists(png)) System.IO.File.Delete(png);
        }

        [Test]
        public void Capture_RendersPrefabAtDesignResolution()
        {
            var (prefab, _) = PrefabFixture.Build();
            var scenes = SceneManager.sceneCount;
            PrefabCapture.Capture(prefab, 2560, 1440, png);
            Assert.AreEqual(scenes, SceneManager.sceneCount, "不能留下或打开别的场景");

            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(png)));
            Assert.AreEqual(2560, tex.width);
            Assert.AreEqual(1440, tex.height);
            // 面板外（左上角）是背景色，面板中间画了东西（Texture2D 的 y 从下往上）
            var bg = tex.GetPixel(10, 1430);
            var panel = tex.GetPixel(700, 1440 - 700);
            Assert.Greater(Mathf.Abs(bg.r - panel.r) + Mathf.Abs(bg.g - panel.g) + Mathf.Abs(bg.b - panel.b), 0.1f);
            Object.DestroyImmediate(tex);
        }
    }
}
