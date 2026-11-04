using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    public class FbxWriterTests
    {
        const string P = "Knight";
        static string TripoFbx => Path.Combine(ModelTestPaths.TripoDir, ModelTestPaths.TripoFbxName);
        static string FbxPath => $"{ModelTestPaths.OutDir}/{P}.fbx";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);

        static ModelImporter Importer => (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        static ModelImporterClipAnimation Clip(string name) => Importer.clipAnimations.Single(c => c.name == name);

        [Test]
        public void First_ClipsNamedAndLooped()
        {
            var r = FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            Assert.AreEqual(FbxPath, r.fbxPath);
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" }, r.clipNames);
            Assert.IsTrue(Clip("idle").loopTime);
            Assert.IsTrue(Clip("walk").loopTime);
            Assert.IsFalse(Clip("sit").loopTime);
            CollectionAssert.AreEquivalent(new[] { "idle", "sit", "walk" },
                AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name));
        }

        [Test]
        public void First_RootMotionInPlace()
        {
            FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            StringAssert.EndsWith("Root", Importer.motionNodeName);
            foreach (var c in Importer.clipAnimations)
            {
                Assert.IsFalse(c.lockRootPositionXZ, $"{c.name}：水平位移要提取成根运动（之后被丢弃），不能 bake 进姿势");
                Assert.IsFalse(c.lockRootRotation, c.name);
                Assert.IsTrue(c.lockRootHeightY, $"{c.name}：竖直位移留在姿势里");
            }
        }

        [Test]
        public void First_GenericUprightAndHeight()
        {
            var r = FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            Assert.AreEqual(ModelImporterAnimationType.Generic, Importer.animationType);
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, Importer.avatarSetup);
            // 实测 Tripo 样例约 0.99 m（不能被 Icosphere 的 2 m 撑大）
            Assert.AreEqual(0.99f, r.heightMeters, 0.05f);
        }

        [Test]
        public void StrayIcosphere_Detected()
        {
            var r = FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            CollectionAssert.AreEqual(new[] { "Icosphere" }, r.strayMeshes);
            Assert.IsTrue(r.warnings.Any(w => w.Contains("Icosphere")));
            CollectionAssert.DoesNotContain(FbxWriter.MaterialSlots(FbxPath, r.strayMeshes), "Lit");
            Assert.AreEqual(1, FbxWriter.MaterialSlots(FbxPath, r.strayMeshes).Count);
        }

        [Test]
        public void Bones_Listed()
        {
            var r = FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            CollectionAssert.Contains(r.bones, "Root");
            CollectionAssert.Contains(r.bones, "Head");
            CollectionAssert.AreEqual(r.bones.OrderBy(b => b, System.StringComparer.Ordinal).Distinct(), r.bones);
        }

        [Test]
        public void Reimport_KeepsClipCustomizations()
        {
            FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            var imp = Importer;
            var clips = imp.clipAnimations;
            clips.Single(c => c.name == "sit").loopTime = true;
            var idle = clips.Single(c => c.name == "idle");
            idle.events = new[] { new AnimationEvent { functionName = "Footstep", time = 0.5f } };
            idle.lockRootPositionXZ = true;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, false);
            Assert.IsTrue(Clip("sit").loopTime, "程序改的循环设置要保留");
            Assert.AreEqual("Footstep", Clip("idle").events.Single().functionName);
            Assert.IsTrue(Clip("idle").lockRootPositionXZ, "程序改的根运动设置要保留");
            Assert.AreEqual(3, Importer.clipAnimations.Length);
        }

        [Test]
        public void Reimport_GuidStable()
        {
            FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            var guid = AssetDatabase.AssetPathToGUID(FbxPath);
            FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, false);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(FbxPath));
        }

        [Test]
        public void RemapMaterials_AssignsToSlots()
        {
            var r = FbxWriter.Write(TripoFbx, ModelTestPaths.OutDir, P, true);
            var slot = FbxWriter.MaterialSlots(FbxPath, r.strayMeshes).Single();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, $"{ModelTestPaths.OutDir}/M.mat");
            FbxWriter.RemapMaterials(FbxPath, new System.Collections.Generic.Dictionary<string, Material> { [slot] = mat });
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Assert.AreEqual($"{ModelTestPaths.OutDir}/M.mat", AssetDatabase.GetAssetPath(model.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial));
        }
    }
}
