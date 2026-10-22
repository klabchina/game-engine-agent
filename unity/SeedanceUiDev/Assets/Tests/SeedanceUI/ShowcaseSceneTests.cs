using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Seedance.UiImport.Tests
{
    public class ShowcaseSceneTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset("Assets/Product");
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
        }

        static GameObject MakePrefab(string name)
        {
            System.IO.Directory.CreateDirectory(TestPaths.OutDir);
            var go = new GameObject(name, typeof(RectTransform));
            go.AddComponent<SeedanceUiRoot>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{TestPaths.OutDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>以 Additive 打开展示场景检查，完了关掉</summary>
        static void Inspect(System.Action<Scene> check)
        {
            var scene = EditorSceneManager.OpenScene(ShowcaseScene.ScenePath, OpenSceneMode.Additive);
            try { check(scene); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        static T Find<T>(Scene s) where T : Component =>
            s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        [Test]
        public void Create_LandscapeScene()
        {
            var report = new ImportReport();
            ShowcaseScene.Show(MakePrefab("A"), 2560, 1440, report);
            Assert.AreEqual(ShowcaseScene.ScenePath, report.scenePath);
            Inspect(s =>
            {
                var canvas = Find<Canvas>(s);
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                var scaler = canvas.GetComponent<CanvasScaler>();
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(2560, 1440), scaler.referenceResolution);
                Assert.AreEqual(0f, scaler.matchWidthOrHeight);
                Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>());
                Assert.IsNotNull(Find<EventSystem>(s));
                Assert.IsNotNull(Find<Camera>(s));
            });
        }

        [Test]
        public void Create_PortraitScene()
        {
            ShowcaseScene.Show(MakePrefab("A"), 1440, 2560, new ImportReport());
            Inspect(s => Assert.AreEqual(1f, Find<CanvasScaler>(s).matchWidthOrHeight));
        }

        [Test]
        public void EventSystemModule_FollowsInputHandling()
        {
            // 开发工程（URP 模板）只开了新 Input System；用 StandaloneInputModule 会每帧报错
            ShowcaseScene.Show(MakePrefab("A"), 2560, 1440, new ImportReport());
            Inspect(s =>
            {
                var module = Find<EventSystem>(s).GetComponent<BaseInputModule>();
                Assert.AreEqual("InputSystemUIInputModule", module.GetType().Name);
            });
        }

        [Test]
        public void Show_TwoPrefabs_OnlyLatestActive()
        {
            var a = MakePrefab("A");
            var b = MakePrefab("B");
            ShowcaseScene.Show(a, 2560, 1440, new ImportReport());
            ShowcaseScene.Show(b, 2560, 1440, new ImportReport());
            Inspect(s =>
            {
                var roots = Find<Canvas>(s).GetComponentsInChildren<SeedanceUiRoot>(true);
                Assert.AreEqual(2, roots.Length);
                Assert.IsTrue(roots.Single(r => r.name == "B").gameObject.activeSelf);
                Assert.IsFalse(roots.Single(r => r.name == "A").gameObject.activeSelf);
            });
            ShowcaseScene.Show(a, 2560, 1440, new ImportReport());
            Inspect(s =>
            {
                var roots = Find<Canvas>(s).GetComponentsInChildren<SeedanceUiRoot>(true);
                Assert.AreEqual(2, roots.Length);
                Assert.IsTrue(roots.Single(r => r.name == "A").gameObject.activeSelf);
                Assert.IsFalse(roots.Single(r => r.name == "B").gameObject.activeSelf);
            });
        }

        [Test]
        public void Show_InstanceIsPrefabLinked()
        {
            var a = MakePrefab("A");
            ShowcaseScene.Show(a, 2560, 1440, new ImportReport());
            Inspect(s =>
            {
                var inst = Find<SeedanceUiRoot>(s).gameObject;
                Assert.AreSame(a, PrefabUtility.GetCorrespondingObjectFromSource(inst));
                Assert.AreEqual("Canvas", inst.transform.parent.name);
            });
        }

        [Test]
        public void Show_ResolutionMismatch_Warns()
        {
            ShowcaseScene.Show(MakePrefab("A"), 2560, 1440, new ImportReport());
            var report = new ImportReport();
            ShowcaseScene.Show(MakePrefab("B"), 1440, 2560, report);
            Assert.AreEqual(1, report.warnings.Count);
            Inspect(s => Assert.AreEqual(new Vector2(2560, 1440), Find<CanvasScaler>(s).referenceResolution));
        }

        [Test]
        public void Show_DoesNotTouchUserScene()
        {
            // 测试运行器自己开着一个未命名场景，没法再叠加新建，所以用户场景先落成文件再打开
            System.IO.Directory.CreateDirectory(TestPaths.OutDir);
            System.IO.File.WriteAllText(TestPaths.OutDir + "/User.unity", "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(TestPaths.OutDir + "/User.unity");
            var user = EditorSceneManager.OpenScene(TestPaths.OutDir + "/User.unity", OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(user);
                new GameObject("UserThing");
                EditorSceneManager.MarkSceneDirty(user);
                Assert.IsTrue(user.isDirty);
                var count = SceneManager.sceneCount;
                ShowcaseScene.Show(MakePrefab("A"), 2560, 1440, new ImportReport());
                Assert.AreEqual(user, SceneManager.GetActiveScene());
                Assert.IsTrue(user.isDirty);
                Assert.AreEqual(count, SceneManager.sceneCount);
                Assert.IsTrue(user.GetRootGameObjects().Any(g => g.name == "UserThing"));
            }
            finally
            {
                EditorSceneManager.CloseScene(user, true);
            }
        }
    }
}
