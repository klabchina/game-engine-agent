using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    public class ModelRecordStoreTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);
            AssetDatabase.DeleteAsset(ModelTestPaths.RecordOutDir);
        }

        static string PrefabPath(string name) => $"{ModelTestPaths.OutDir}/{name}.prefab";

        /// <summary>存一个空 Prefab，返回 GUID</summary>
        static string MakePrefab(string name)
        {
            Directory.CreateDirectory(ModelTestPaths.OutDir);
            var go = new GameObject(name);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(name));
            Object.DestroyImmediate(go);
            return AssetDatabase.AssetPathToGUID(PrefabPath(name));
        }

        static ModelRecord Sample(string guid) => new ModelRecord
        {
            prefabGuid = guid, fbxGuid = "f00", source = "tripo",
            clips = { "idle", "walk" },
            textures = { new RoleGuid { role = "BaseColor", guid = "b00" } },
            materials = { "m00" },
            bones = { "Root", "Spine01" },
        };

        static void WriteRaw(string recordPath, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(recordPath));
            File.WriteAllText(recordPath, content);
            AssetDatabase.ImportAsset(recordPath);
        }

        [Test]
        public void RecordPath_Mirrors() =>
            Assert.AreEqual("Assets/SeedanceModel/Characters/Knight/Knight.json",
                ModelRecordStore.RecordPathFor("Assets/Characters/Knight/Knight.prefab"));

        [Test]
        public void RecordPath_NonAsciiAndSpaces() =>
            Assert.AreEqual("Assets/SeedanceModel/角色 模型/X.json", ModelRecordStore.RecordPathFor("Assets/角色 模型/X.prefab"));

        [Test]
        public void SaveThenFind_RoundTrip()
        {
            var guid = MakePrefab("A");
            ModelRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            var r = ModelRecordStore.FindByGuid(guid, PrefabPath("A"), out var at);
            Assert.AreEqual(ModelRecordStore.RecordPathFor(PrefabPath("A")), at);
            Assert.AreEqual(1, r.formatVersion);
            Assert.AreEqual("f00", r.fbxGuid);
            Assert.AreEqual("tripo", r.source);
            CollectionAssert.AreEqual(new[] { "idle", "walk" }, r.clips);
            Assert.AreEqual("BaseColor", r.textures[0].role);
            Assert.AreEqual("b00", r.textures[0].guid);
            CollectionAssert.AreEqual(new[] { "m00" }, r.materials);
            CollectionAssert.AreEqual(new[] { "Root", "Spine01" }, r.bones);
            var dir = Path.GetDirectoryName(at);
            Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"), "临时文件要清掉");
        }

        [Test]
        public void Find_None_ReturnsNull()
        {
            var guid = MakePrefab("A");
            Assert.IsNull(ModelRecordStore.FindByGuid(guid, PrefabPath("A"), out var at));
            Assert.IsNull(at);
        }

        [Test]
        public void Find_PrefabMoved_FindsByGuid()
        {
            var guid = MakePrefab("A");
            ModelRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath("A"), PrefabPath("B")));
            var r = ModelRecordStore.FindByGuid(guid, PrefabPath("B"), out var at);
            Assert.IsNotNull(r);
            Assert.AreEqual(ModelRecordStore.RecordPathFor(PrefabPath("A")), at);
        }

        [Test]
        public void Save_WithPreviousPath_MovesRecord()
        {
            var guid = MakePrefab("A");
            ModelRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            var old = ModelRecordStore.RecordPathFor(PrefabPath("A"));
            ModelRecordStore.Save(PrefabPath("B"), Sample(guid), old);
            Assert.IsFalse(File.Exists(old));
            Assert.IsFalse(File.Exists(old + ".meta"));
            Assert.IsTrue(File.Exists(ModelRecordStore.RecordPathFor(PrefabPath("B"))));
        }

        [Test]
        public void Find_MirrorPathGuidMismatch_UsesGuid()
        {
            var guidA = MakePrefab("A");
            var guidB = MakePrefab("B");
            // A 的镜像路径上放的是 B 的记录；A 真正的记录在别处
            WriteRaw(ModelRecordStore.RecordPathFor(PrefabPath("A")), JsonUtility.ToJson(Sample(guidB)));
            var realA = Sample(guidA);
            realA.source = "hunyuan";
            WriteRaw(ModelRecordStore.RecordPathFor(PrefabPath("Elsewhere/A")), JsonUtility.ToJson(realA));
            var r = ModelRecordStore.FindByGuid(guidA, PrefabPath("A"), out var at);
            Assert.AreEqual("hunyuan", r.source);
            Assert.AreEqual(ModelRecordStore.RecordPathFor(PrefabPath("Elsewhere/A")), at);
        }

        [Test]
        public void Find_Corrupt_Throws()
        {
            var guid = MakePrefab("A");
            var path = ModelRecordStore.RecordPathFor(PrefabPath("A"));
            WriteRaw(path, "{not json");
            var ex = Assert.Throws<ModelImportException>(() => ModelRecordStore.FindByGuid(guid, PrefabPath("A"), out _));
            StringAssert.Contains(path, ex.Message);
        }

        [Test]
        public void Find_DuplicateGuid_Throws()
        {
            var guid = MakePrefab("A");
            var p1 = ModelRecordStore.RecordPathFor(PrefabPath("X"));
            var p2 = ModelRecordStore.RecordPathFor(PrefabPath("Y"));
            WriteRaw(p1, JsonUtility.ToJson(Sample(guid)));
            WriteRaw(p2, JsonUtility.ToJson(Sample(guid)));
            var ex = Assert.Throws<ModelImportException>(() => ModelRecordStore.FindByGuid(guid, PrefabPath("A"), out _));
            StringAssert.Contains(p1, ex.Message);
            StringAssert.Contains(p2, ex.Message);
        }

        [Test]
        public void ReleaseMirrorPath_MovesRecordOfMovedPrefab()
        {
            var guid = MakePrefab("X");
            ModelRecordStore.Save(PrefabPath("X"), Sample(guid), null);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath("X"), PrefabPath("Y")));
            ModelRecordStore.ReleaseMirrorPath(PrefabPath("X"));
            Assert.IsFalse(File.Exists(ModelRecordStore.RecordPathFor(PrefabPath("X"))));
            Assert.IsTrue(File.Exists(ModelRecordStore.RecordPathFor(PrefabPath("Y"))));
            Assert.IsNotNull(ModelRecordStore.FindByGuid(guid, PrefabPath("Y"), out _));
        }

        [Test]
        public void Save_CaseOnlyRename_KeepsRecord()
        {
            var guid = MakePrefab("bag");
            ModelRecordStore.Save(PrefabPath("bag"), Sample(guid), null);
            // 程序只改了 Prefab 名的大小写（bag → Bag），重导时按 GUID 找到旧记录再搬过去
            ModelRecordStore.Save(PrefabPath("Bag"), Sample(guid), ModelRecordStore.RecordPathFor(PrefabPath("bag")));
            Assert.IsNotNull(ModelRecordStore.FindByGuid(guid, PrefabPath("Bag"), out var at), "记录不能被当成旧文件删掉");
            StringAssert.EndsWith("/Bag.json", at);
        }

        [Test]
        public void ValidateAll_DuplicateGuid_Throws()
        {
            var guid = MakePrefab("A");
            WriteRaw(ModelRecordStore.RecordPathFor(PrefabPath("X")), JsonUtility.ToJson(Sample(guid)));
            WriteRaw(ModelRecordStore.RecordPathFor(PrefabPath("Y")), JsonUtility.ToJson(Sample(guid)));
            Assert.Throws<ModelImportException>(ModelRecordStore.ValidateAll);
        }

        [Test]
        public void ReleaseMirrorPath_StaleRecord_LeftForOverwrite()
        {
            var path = ModelRecordStore.RecordPathFor(PrefabPath("X"));
            WriteRaw(path, JsonUtility.ToJson(Sample("00000000000000000000000000000000")));
            ModelRecordStore.ReleaseMirrorPath(PrefabPath("X"));
            Assert.IsTrue(File.Exists(path));
        }
    
        [Test]
        public void EnsureWritable_ReadOnly_Throws()
        {
            var guid = MakePrefab("A");
            ModelRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            var path = ModelRecordStore.RecordPathFor(PrefabPath("A"));
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try
            {
                var ex = Assert.Throws<ModelImportException>(() => ModelRecordStore.EnsureWritable(PrefabPath("A"), null));
                StringAssert.Contains("只读", ex.Message);
            }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        }

        [Test]
        public void Find_Corrupt_MessageMentionsModelRecordDir()
        {
            var path = ModelRecordStore.RecordPathFor(PrefabPath("A"));
            WriteRaw(path, "{}");
            var ex = Assert.Throws<ModelImportException>(ModelRecordStore.ValidateAll);
            StringAssert.Contains("Assets/SeedanceModel/", ex.Message);
        }
    }
}
