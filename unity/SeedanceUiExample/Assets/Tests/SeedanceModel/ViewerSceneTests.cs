using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seedance.ModelImport.Tests
{
    public class ViewerSceneTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset("Assets/Product");
            AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);
            AssetDatabase.DeleteAsset(ModelTestPaths.RecordOutDir);
        }

        /// <summary>存一个带 1x2x1 方块的 Prefab；withRecord 时写一份导入记录（= 导入出来的模型）</summary>
        static GameObject MakePrefab(string name, bool withRecord = true)
        {
            Directory.CreateDirectory(ModelTestPaths.OutDir);
            var root = new GameObject(name);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = new Vector3(1, 2, 1);
            cube.transform.localPosition = new Vector3(0, 1, 0);
            var path = $"{ModelTestPaths.OutDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (withRecord)
                ModelRecordStore.Save(path, new ModelRecord { prefabGuid = AssetDatabase.AssetPathToGUID(path) }, null);
            return prefab;
        }

        static void Inspect(System.Action<Scene> check)
        {
            var scene = EditorSceneManager.OpenScene(ViewerScene.ScenePath, OpenSceneMode.Additive);
            try { check(scene); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        static T Find<T>(Scene s) where T : Component =>
            s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

        static GameObject[] Instances(Scene s) =>
            s.GetRootGameObjects().Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g)).ToArray();

        [Test]
        public void Show_CreatesSceneWithGroundLightCamera()
        {
            var report = new ModelImportReport();
            ViewerScene.Show(MakePrefab("A"), report);
            Assert.AreEqual(ViewerScene.ScenePath, report.scenePath);
            Inspect(s =>
            {
                Assert.IsTrue(s.GetRootGameObjects().Any(g => g.name == "Ground"));
                Assert.AreEqual(LightType.Directional, Find<Light>(s).type);
                var cam = Find<Camera>(s);
                Assert.IsNotNull(cam);
                Assert.Greater(Vector3.Dot(cam.transform.forward, (new Vector3(0, 1, 0) - cam.transform.position).normalized), 0.95f, "相机要对着模型");
                var inst = Instances(s).Single();
                Assert.AreEqual(Vector3.zero, inst.transform.position);
            });
        }

        [Test]
        public void Show_TwoModels_OnlyLatestActive()
        {
            var a = MakePrefab("A");
            var b = MakePrefab("B");
            ViewerScene.Show(a, new ModelImportReport());
            ViewerScene.Show(b, new ModelImportReport());
            Inspect(s =>
            {
                Assert.IsTrue(Instances(s).Single(g => g.name == "B").activeSelf);
                Assert.IsFalse(Instances(s).Single(g => g.name == "A").activeSelf);
            });
        }

        [Test]
        public void Show_SamePrefabTwice_OneInstance()
        {
            var a = MakePrefab("A");
            ViewerScene.Show(a, new ModelImportReport());
            ViewerScene.Show(a, new ModelImportReport());
            Inspect(s => Assert.AreEqual(1, Instances(s).Length));
        }

        [Test]
        public void Show_IgnoresManualObject()
        {
            ViewerScene.Show(MakePrefab("A"), new ModelImportReport());
            var manual = MakePrefab("Manual", withRecord: false);
            var scene = EditorSceneManager.OpenScene(ViewerScene.ScenePath, OpenSceneMode.Additive);
            PrefabUtility.InstantiatePrefab(manual, scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);

            ViewerScene.Show(MakePrefab("B"), new ModelImportReport());
            Inspect(s =>
            {
                Assert.IsTrue(Instances(s).Single(g => g.name == "Manual").activeSelf, "不是导入的模型，不能被隐藏");
                Assert.IsFalse(Instances(s).Single(g => g.name == "A").activeSelf);
            });
        }

        [Test]
        public void Show_DoesNotTouchUserScene()
        {
            Directory.CreateDirectory(ModelTestPaths.OutDir);
            File.WriteAllText(ModelTestPaths.OutDir + "/User.unity", "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(ModelTestPaths.OutDir + "/User.unity");
            var user = EditorSceneManager.OpenScene(ModelTestPaths.OutDir + "/User.unity", OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(user);
                new GameObject("UserThing");
                EditorSceneManager.MarkSceneDirty(user);
                var count = SceneManager.sceneCount;
                ViewerScene.Show(MakePrefab("A"), new ModelImportReport());
                Assert.AreEqual(user, SceneManager.GetActiveScene());
                Assert.IsTrue(user.isDirty);
                Assert.AreEqual(count, SceneManager.sceneCount);
            }
            finally { EditorSceneManager.CloseScene(user, true); }
        }
    }
}
