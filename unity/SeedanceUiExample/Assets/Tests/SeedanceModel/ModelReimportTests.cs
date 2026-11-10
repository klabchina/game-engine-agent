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

        static string SmrMaterialPath() =>
            AssetDatabase.GetAssetPath(Prefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial);

        [Test]
        public void Reimport_SameSource_KeepsMaterial()
        {
            SeedanceModelImporter.Run(Req());
            SeedanceModelImporter.Run(Req());
            Assert.AreEqual(ImportDir + "/Materials/Knight.mat", SmrMaterialPath(), "第二次导入材质不能掉");
            SeedanceModelImporter.Run(Req());
            Assert.AreEqual(ImportDir + "/Materials/Knight.mat", SmrMaterialPath(), "第三次也不能掉");
        }

        [Test]
        public void Reimport_ProgrammerController_NotTouched()
        {
            SeedanceModelImporter.Run(Req());
            var minePath = ModelTestPaths.OutDir + "/Mine.controller";
            var mine = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(minePath);
            mine.layers[0].stateMachine.AddState("idle");
            AssetDatabase.SaveAssets();
            EditPrefab(root => ModelNode(root).GetComponent<Animator>().runtimeAnimatorController = mine);

            var report = SeedanceModelImporter.Run(Req());
            Assert.AreEqual(minePath, AssetDatabase.GetAssetPath(Prefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController), "程序换上的 Controller 不能被换回去");
            var mine2 = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(minePath);
            Assert.AreEqual(1, mine2.layers[0].stateMachine.states.Length, "不能往程序的 Controller 里加状态");
            Assert.IsNull(mine2.layers[0].stateMachine.states[0].state.motion, "不能改程序 Controller 里的状态");
            Assert.IsTrue(report.warnings.Any(w => w.Contains("Controller")));
        }

        [Test]
        public void FirstImport_FbxOwnedByRenamedPrefab_Throws()
        {
            SeedanceModelImporter.Run(Req());
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath, ImportDir + "/Hero.prefab"));
            var fbxBefore = System.IO.File.GetLastWriteTimeUtc(FbxPath);
            var ex = Assert.Throws<ModelImportException>(() => SeedanceModelImporter.Run(Req()));
            StringAssert.Contains("Hero.prefab", ex.Message);
            Assert.AreEqual(fbxBefore, System.IO.File.GetLastWriteTimeUtc(FbxPath), "别人的 FBX 不能被覆盖");
            Assert.IsFalse(System.IO.File.Exists(PrefabPath));
        }

        [Test]
        public void FirstImport_AfterPrefabDeleted_ClaimsExistingStates()
        {
            // SKILL.md：记录 / Prefab 坏了时“删掉那个 Prefab 再导入会按首次导入重建”——同名 Controller 还在
            SeedanceModelImporter.Run(Req());
            Assert.IsTrue(AssetDatabase.DeleteAsset(PrefabPath));
            var report = SeedanceModelImporter.Run(Req());
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" }, report.updatedStates);
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" }, Record().clips);
            Assert.IsFalse(report.warnings.Any(w => w.Contains("程序建的状态")));
        }

        [Test]
        public void Attachments_UnderRemovedBones_Named()
        {
            SeedanceModelImporter.Run(Req());
            EditPrefab(root => new GameObject("Weapon").transform.SetParent(FindDeep(root.transform, "Spine01"), false));
            var hits = ModelPrefabBuilder.AttachmentsUnder(PrefabPath, new System.Collections.Generic.HashSet<string> { "Spine01" });
            CollectionAssert.AreEqual(new[] { "Weapon（Spine01）" }, hits);
            CollectionAssert.IsEmpty(ModelPrefabBuilder.AttachmentsUnder(PrefabPath, new System.Collections.Generic.HashSet<string> { "Head" }));
        }
    }
}
