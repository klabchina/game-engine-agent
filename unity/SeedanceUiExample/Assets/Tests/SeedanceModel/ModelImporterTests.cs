using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;
using static Seedance.ModelImport.Tests.ModelImportFixture;

namespace Seedance.ModelImport.Tests
{
    public class ModelImporterTests
    {
        [TearDown]
        public void TearDown() => CleanUp();

        static bool OutDirHasFiles() =>
            Directory.Exists(ModelTestPaths.OutDir) &&
            Directory.GetFiles(ModelTestPaths.OutDir, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta") && !f.EndsWith("Existing.prefab"));

        [Test]
        public void Run_Tripo_FirstImport()
        {
            var report = SeedanceModelImporter.Run(Req());
            Assert.AreEqual(PrefabPath, report.prefabPath);
            Assert.AreEqual(ControllerPath, report.controllerPath);
            foreach (var f in new[] { FbxPath, ControllerPath, PrefabPath, ImportDir + "/Materials/Knight.mat",
                         ImportDir + "/Textures/Knight_BaseColor.jpg", ImportDir + "/Textures/Knight_Normal.jpg", ImportDir + "/Textures/Knight_Metallic.png" })
                Assert.IsTrue(File.Exists(f), f);

            var animator = Prefab.GetComponentsInChildren<Animator>(true).Single();
            Assert.AreEqual(ControllerPath, AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            Assert.IsNotNull(animator.avatar);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsEmpty(Prefab.GetComponentsInChildren<Component>(true).Where(c => c.GetType().FullName.StartsWith("Seedance.")));

            var record = Record();
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" }, record.clips);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(PrefabPath), record.prefabGuid);
            Assert.AreEqual(3, record.textures.Count);
            Assert.AreEqual("tripo", record.source);
            CollectionAssert.Contains(report.clips, "idle (loop)");
            CollectionAssert.Contains(report.clips, "sit");
            Assert.AreEqual(0.99f, report.heightMeters, 0.05f);
        }

        [Test]
        public void Run_StrayIcosphereInactive_MaterialOnCharacter()
        {
            SeedanceModelImporter.Run(Req());
            var ico = FindDeep(Prefab.transform, "Icosphere");
            Assert.IsNotNull(ico);
            Assert.IsFalse(ico.gameObject.activeSelf);
            var smr = Prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(ImportDir + "/Materials/Knight.mat", AssetDatabase.GetAssetPath(smr.sharedMaterial));
        }

        /// <summary>
        /// 两份样例的 walk 本身就是原地动画（根骨骼没有水平位移曲线），这个用例分不出“bake / 不 bake 水平位移”；
        /// 真正把关原地播设置的是 FbxWriterTests.First_RootMotionInPlace。这里只保证 Prefab 能正常播 walk、根不动。
        /// </summary>
        [Test]
        public void Run_WalkPlaysInPlace()
        {
            SeedanceModelImporter.Run(Req());
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(Prefab);
            try
            {
                var animator = inst.GetComponentInChildren<Animator>();
                animator.Play("walk", 0, 0f);
                animator.Update(0f);
                var hip = FindDeep(inst.transform, "Hip");
                var start = hip.position;
                var maxDrift = 0f;
                for (var i = 0; i < 60; i++)
                {
                    animator.Update(1f / 24f);
                    maxDrift = Mathf.Max(maxDrift, Vector2.Distance(new Vector2(hip.position.x, hip.position.z), new Vector2(start.x, start.z)));
                }
                Assert.Less(maxDrift, 0.15f, "走路要原地播");
                Assert.AreEqual(Vector3.zero, inst.transform.position);
            }
            finally { Object.DestroyImmediate(inst); }
        }

        [Test]
        public void Run_PlayMode_Throws_WritesNothing()
        {
            SeedanceModelImporter.IsPlaying = () => true;
            var ex = Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(Req()));
            StringAssert.Contains("Play Mode", ex.Message);
            Assert.IsFalse(OutDirHasFiles());
        }

        [Test]
        public void Run_BadPrefabName_Throws_WritesNothing()
        {
            var req = Req();
            req.prefabName = "骑士";
            Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(req));
            req.prefabName = "";
            req.importDir = "Packages/X";
            Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(req));
            Assert.IsFalse(OutDirHasFiles());
        }

        [Test]
        public void Run_PrefabWithoutRecord_Throws_WritesNothing()
        {
            Directory.CreateDirectory(ImportDir);
            var go = new GameObject("Knight");
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            var ex = Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(Req()));
            StringAssert.Contains("找不到它的导入记录", ex.Message);
            Assert.IsFalse(File.Exists(FbxPath));
            Assert.IsFalse(Directory.Exists(ImportDir + "/Textures"));
        }

        [Test]
        public void Run_BrokenRecordElsewhere_Throws_WritesNothing()
        {
            var broken = ModelTestPaths.RecordOutDir + "/Broken.json";
            Directory.CreateDirectory(ModelTestPaths.RecordOutDir);
            File.WriteAllText(broken, "{oops");
            AssetDatabase.ImportAsset(broken);
            var ex = Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(Req()));
            StringAssert.Contains(broken, ex.Message);
            Assert.IsFalse(OutDirHasFiles());
        }

        [Test]
        public void Run_DefaultPrefabNameFromDir()
        {
            var report = SeedanceModelImporter.Run(Req(ModelTestPaths.TripoDir));
            Assert.AreEqual(PrefabPath, report.prefabPath);
        }
    }
}
