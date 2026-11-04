using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;
using static Seedance.ModelImport.Tests.ModelImportFixture;

namespace Seedance.ModelImport.Tests
{
    public class ModelReimportTests
    {
        string scratch;

        [SetUp]
        public void SetUp() => scratch = NewScratch();

        [TearDown]
        public void TearDown()
        {
            CleanUp();
            if (System.IO.Directory.Exists(scratch)) System.IO.Directory.Delete(scratch, true);
        }

        [Test]
        public void Reimport_KeepsBoneAttachmentAndClipEvents()
        {
            SeedanceModelImporter.Run(Req());
            EditPrefab(root =>
            {
                root.AddComponent<BoxCollider>();
                new GameObject("Weapon").transform.SetParent(FindDeep(root.transform, "Spine01"), false);
            });
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            var clips = imp.clipAnimations;
            clips.Single(c => c.name == "idle").events = new[] { new AnimationEvent { functionName = "Blink", time = 0.2f } };
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            var smrCount = Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;

            var report = SeedanceModelImporter.Run(Req(ModelTestPaths.TripoDir));
            Assert.IsNotNull(Prefab.GetComponent<BoxCollider>(), "根上程序加的组件要保留");
            var weapon = FindDeep(Prefab.transform, "Weapon");
            Assert.IsNotNull(weapon, "骨骼下的挂点要保留");
            Assert.AreEqual("Spine01", weapon.parent.name);
            Assert.AreEqual("Blink", ((ModelImporter)AssetImporter.GetAtPath(FbxPath)).clipAnimations.Single(c => c.name == "idle").events.Single().functionName);
            Assert.AreEqual(smrCount, Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, "不能出现第二份模型");
            Assert.AreEqual(1, Prefab.GetComponentsInChildren<Animator>(true).Length);
            CollectionAssert.IsEmpty(report.createdStates);
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" }, report.updatedStates);
            CollectionAssert.IsEmpty(report.missingClips);
            CollectionAssert.IsEmpty(report.removedBones);
        }

        [Test]
        public void Reimport_DifferentSource_ReportsAndKeepsRootComponents()
        {
            SeedanceModelImporter.Run(Req());
            EditPrefab(root => root.AddComponent<BoxCollider>());
            // 模拟“上次的 FBX 还有 jump 动画、OldBone 骨骼”
            Controller.layers[0].stateMachine.AddState("jump");
            AssetDatabase.SaveAssets();
            var r = Record();
            r.clips.Add("jump");
            r.bones.Add("OldBone");
            WriteRecord(r);

            var report = SeedanceModelImporter.Run(Req(SingleTextureSource(scratch)));
            CollectionAssert.Contains(report.missingClips, "jump");
            CollectionAssert.Contains(report.removedBones, "OldBone");
            CollectionAssert.Contains(report.orphanTextures, ImportDir + "/Textures/Knight_Normal.jpg");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ImportDir + "/Materials/Knight.mat");
            Assert.IsNull(mat.GetTexture("_BumpMap"));
            Assert.IsNull(mat.GetTexture("_MetallicGlossMap"));
            Assert.AreEqual(ImportDir + "/Textures/Knight_BaseColor.png", AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap")));
            Assert.IsNotNull(Prefab.GetComponent<BoxCollider>());
            Assert.AreEqual("hunyuan", Record().source);
            CollectionAssert.Contains(Record().clips, "jump", "missing 的动画留在记录里，以后回来还能接上");
        }

        [Test]
        public void Reimport_PrefabMoved_RecordFollows()
        {
            SeedanceModelImporter.Run(Req());
            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath, ImportDir + "/Hero.prefab"));
            var req = Req();
            req.prefabName = "Hero";
            SeedanceModelImporter.Run(req);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(ImportDir + "/Hero.prefab"));
            Assert.IsNotNull(ModelRecordStore.FindByGuid(guid, ImportDir + "/Hero.prefab", out var at));
            Assert.AreEqual(ModelRecordStore.RecordPathFor(ImportDir + "/Hero.prefab"), at);
        }
    
        static Transform ModelNode(GameObject root) => root.GetComponentInChildren<Animator>(true).transform;

        [Test]
        public void Reimport_FacingFollowsUpstream_WhenNotRotatedByProgrammer()
        {
            SeedanceModelImporter.Run(Req());
            // 模拟“上次的 FBX 本来就朝 +Z”：记录 0°、Model 子节点也是 0°
            EditPrefab(root => ModelNode(root).localRotation = Quaternion.identity);
            var r = Record();
            r.facingYaw = 0f;
            WriteRecord(r);
            SeedanceModelImporter.Run(Req());
            Assert.AreEqual(270f, ModelNode(Prefab).localEulerAngles.y, 0.5f);
            Assert.AreEqual(-90f, Record().facingYaw);
        }

        [Test]
        public void Reimport_FacingKept_WhenProgrammerRotated()
        {
            SeedanceModelImporter.Run(Req());
            EditPrefab(root => ModelNode(root).localRotation = Quaternion.Euler(0, 45, 0));
            var r = Record();
            r.facingYaw = 0f;
            WriteRecord(r);
            var report = SeedanceModelImporter.Run(Req());
            Assert.AreEqual(45f, ModelNode(Prefab).localEulerAngles.y, 0.5f, "程序转过的不动");
            Assert.IsTrue(report.warnings.Any(w => w.Contains("朝向")));
        }
    }
}
