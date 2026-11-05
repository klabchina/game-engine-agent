using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class UiRecordStoreTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
            AssetDatabase.DeleteAsset(TestPaths.RecordOutDir);
        }

        static string PrefabPath(string name) => $"{TestPaths.OutDir}/{name}.prefab";

        /// <summary>存一个空 RectTransform Prefab，返回 GUID</summary>
        static string MakePrefab(string name)
        {
            Directory.CreateDirectory(TestPaths.OutDir);
            var go = new GameObject(name, typeof(RectTransform));
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(name));
            Object.DestroyImmediate(go);
            return AssetDatabase.AssetPathToGUID(PrefabPath(name));
        }

        static UiRecord Sample(string guid) => new UiRecord
        {
            prefabGuid = guid, designWidth = 2560, designHeight = 1440, layoutVersion = 1,
            elements =
            {
                new UiRecordElement { elementId = "e1", fileId = 1234567890123L, sourceName = "面板", type = "panel", layoutParentId = UiRecordStore.RootParentId },
                new UiRecordElement { elementId = "e2", fileId = -42L, sourceName = "升级", type = "button", layoutParentId = "e1" },
            },
        };

        static void WriteRaw(string recordPath, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(recordPath));
            File.WriteAllText(recordPath, content);
            AssetDatabase.ImportAsset(recordPath);
        }

        [Test]
        public void RecordPath_Mirrors() =>
            Assert.AreEqual("Assets/SeedanceUI/UI/Generated/CharacterPanel.json",
                UiRecordStore.RecordPathFor("Assets/UI/Generated/CharacterPanel.prefab"));

        [Test]
        public void RecordPath_NonAsciiAndSpaces() =>
            Assert.AreEqual("Assets/SeedanceUI/UI 界面/X.json", UiRecordStore.RecordPathFor("Assets/UI 界面/X.prefab"));

        [Test]
        public void SaveThenFind_RoundTrip()
        {
            var guid = MakePrefab("A");
            UiRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            var r = UiRecordStore.FindByGuid(guid, PrefabPath("A"), out var at);
            Assert.AreEqual(UiRecordStore.RecordPathFor(PrefabPath("A")), at);
            Assert.AreEqual(1, r.formatVersion);
            Assert.AreEqual(2560, r.designWidth);
            Assert.AreEqual(1440, r.designHeight);
            Assert.AreEqual(2, r.elements.Count);
            Assert.AreEqual(1234567890123L, r.elements[0].fileId);
            Assert.AreEqual("<root>", r.elements[0].layoutParentId);
            Assert.AreEqual("升级", r.elements[1].sourceName);
            Assert.AreEqual(-42L, r.elements[1].fileId);
            var dir = Path.GetDirectoryName(at);
            Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"), "临时文件要清掉");
        }

        [Test]
        public void Find_None_ReturnsNull()
        {
            var guid = MakePrefab("A");
            Assert.IsNull(UiRecordStore.FindByGuid(guid, PrefabPath("A"), out var at));
            Assert.IsNull(at);
        }

        [Test]
        public void Find_PrefabMoved_FindsByGuid()
        {
            var guid = MakePrefab("A");
            UiRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath("A"), PrefabPath("B")));
            var r = UiRecordStore.FindByGuid(guid, PrefabPath("B"), out var at);
            Assert.IsNotNull(r);
            Assert.AreEqual(UiRecordStore.RecordPathFor(PrefabPath("A")), at);
        }

        [Test]
        public void Save_WithPreviousPath_MovesRecord()
        {
            var guid = MakePrefab("A");
            UiRecordStore.Save(PrefabPath("A"), Sample(guid), null);
            var old = UiRecordStore.RecordPathFor(PrefabPath("A"));
            UiRecordStore.Save(PrefabPath("B"), Sample(guid), old);
            Assert.IsFalse(File.Exists(old));
            Assert.IsFalse(File.Exists(old + ".meta"));
            Assert.IsTrue(File.Exists(UiRecordStore.RecordPathFor(PrefabPath("B"))));
        }

        [Test]
        public void Find_MirrorPathGuidMismatch_UsesGuid()
        {
            var guidA = MakePrefab("A");
            var guidB = MakePrefab("B");
            // A 的镜像路径上放的是 B 的记录；A 真正的记录在别处
            WriteRaw(UiRecordStore.RecordPathFor(PrefabPath("A")), JsonUtility.ToJson(Sample(guidB)));
            var realA = Sample(guidA);
            realA.designWidth = 1080;
            WriteRaw(UiRecordStore.RecordPathFor(PrefabPath("Elsewhere/A")), JsonUtility.ToJson(realA));
            var r = UiRecordStore.FindByGuid(guidA, PrefabPath("A"), out var at);
            Assert.AreEqual(1080, r.designWidth);
            Assert.AreEqual(UiRecordStore.RecordPathFor(PrefabPath("Elsewhere/A")), at);
        }

        [Test]
        public void Find_Corrupt_Throws()
        {
            var guid = MakePrefab("A");
            var path = UiRecordStore.RecordPathFor(PrefabPath("A"));
            WriteRaw(path, "{not json");
            var ex = Assert.Throws<UiImportException>(() => UiRecordStore.FindByGuid(guid, PrefabPath("A"), out _));
            StringAssert.Contains(path, ex.Message);
        }

        [Test]
        public void Find_DuplicateGuid_Throws()
        {
            var guid = MakePrefab("A");
            var p1 = UiRecordStore.RecordPathFor(PrefabPath("X"));
            var p2 = UiRecordStore.RecordPathFor(PrefabPath("Y"));
            WriteRaw(p1, JsonUtility.ToJson(Sample(guid)));
            WriteRaw(p2, JsonUtility.ToJson(Sample(guid)));
            var ex = Assert.Throws<UiImportException>(() => UiRecordStore.FindByGuid(guid, PrefabPath("A"), out _));
            StringAssert.Contains(p1, ex.Message);
            StringAssert.Contains(p2, ex.Message);
        }

        [Test]
        public void ReleaseMirrorPath_MovesRecordOfMovedPrefab()
        {
            var guid = MakePrefab("X");
            UiRecordStore.Save(PrefabPath("X"), Sample(guid), null);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath("X"), PrefabPath("Y")));
            UiRecordStore.ReleaseMirrorPath(PrefabPath("X"));
            Assert.IsFalse(File.Exists(UiRecordStore.RecordPathFor(PrefabPath("X"))));
            Assert.IsTrue(File.Exists(UiRecordStore.RecordPathFor(PrefabPath("Y"))));
            Assert.IsNotNull(UiRecordStore.FindByGuid(guid, PrefabPath("Y"), out _));
        }

        [Test]
        public void Save_CaseOnlyRename_KeepsRecord()
        {
            var guid = MakePrefab("bag");
            UiRecordStore.Save(PrefabPath("bag"), Sample(guid), null);
            // 程序只改了 Prefab 名的大小写（bag → Bag），重导时按 GUID 找到旧记录再搬过去
            UiRecordStore.Save(PrefabPath("Bag"), Sample(guid), UiRecordStore.RecordPathFor(PrefabPath("bag")));
            Assert.IsNotNull(UiRecordStore.FindByGuid(guid, PrefabPath("Bag"), out var at), "记录不能被当成旧文件删掉");
            StringAssert.EndsWith("/Bag.json", at);
        }

        [Test]
        public void ValidateAll_DuplicateGuid_Throws()
        {
            var guid = MakePrefab("A");
            WriteRaw(UiRecordStore.RecordPathFor(PrefabPath("X")), JsonUtility.ToJson(Sample(guid)));
            WriteRaw(UiRecordStore.RecordPathFor(PrefabPath("Y")), JsonUtility.ToJson(Sample(guid)));
            Assert.Throws<UiImportException>(UiRecordStore.ValidateAll);
        }

        [Test]
        public void ReleaseMirrorPath_StaleRecord_LeftForOverwrite()
        {
            var path = UiRecordStore.RecordPathFor(PrefabPath("X"));
            WriteRaw(path, JsonUtility.ToJson(Sample("00000000000000000000000000000000")));
            UiRecordStore.ReleaseMirrorPath(PrefabPath("X"));
            Assert.IsTrue(File.Exists(path));
        }
    }
}
