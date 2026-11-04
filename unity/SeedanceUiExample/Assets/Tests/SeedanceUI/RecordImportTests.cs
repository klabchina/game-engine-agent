using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;
using static Seedance.UiImport.Tests.PrefabFixture;

namespace Seedance.UiImport.Tests
{
    /// <summary>结构信息存在记录文件里（Prefab 上不挂组件）之后，程序手改 Prefab 的各种情况下重导是否还认得出节点</summary>
    public class RecordImportTests
    {
        [SetUp]
        public void SetUp() => Build();

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestPaths.OutDir);
            AssetDatabase.DeleteAsset(TestPaths.RecordOutDir);
        }

        static string Label(string id) => ImportReport.Label(Doc().Find(id));
        static string RecordPath => UiRecordStore.RecordPathFor(PrefabPath);

        [Test]
        public void Update_RenamedAndReparentedUnderManualNode_Recognized()
        {
            EditPrefab(r =>
            {
                var manual = new GameObject("Manual", typeof(RectTransform));
                manual.transform.SetParent(r.transform.Find("panel_e1"), false);
                var btn = r.transform.Find("panel_e1/button_e2");
                btn.name = "BtnLevelUp";
                btn.SetParent(manual.transform, false);
            });
            var (p, report) = Build();
            CollectionAssert.Contains(report.moved, Label("e2"));
            CollectionAssert.DoesNotContain(report.created, Label("e2"));
            var all = p.GetComponentsInChildren<Transform>(true);
            Assert.AreEqual(1, all.Count(t => t.name == "BtnLevelUp"));
            Assert.AreEqual(0, all.Count(t => t.name == "button_e2"), "不能重建出一个新的 button_e2");
        }

        [Test]
        public void Update_DeletedNode_Recreated()
        {
            EditPrefab(r => Object.DestroyImmediate(r.transform.Find("panel_e1/text_e3").gameObject));
            var (p, report) = Build();
            CollectionAssert.Contains(report.created, Label("e3"));
            Assert.IsNotNull(p.transform.Find("panel_e1/text_e3"));
            Assert.AreSame(p.transform.Find("panel_e1/text_e3").gameObject, NodeOf(p, "e3"));
        }

        [Test]
        public void Update_DuplicatedNode_Untouched()
        {
            EditPrefab(r =>
            {
                var btn = r.transform.Find("panel_e1/button_e2");
                var copy = Object.Instantiate(btn.gameObject, btn.parent);
                copy.name = "Copy";
                ((RectTransform)copy.transform).anchoredPosition = new Vector2(1, 1);
            });
            var doc = Doc();
            doc.elements.RemoveAll(e => e.id == "e2");
            var (p, _) = Build(doc);
            var copyNode = p.transform.Find("panel_e1/Copy");
            Assert.AreEqual(new Vector2(1, 1), ((RectTransform)copyNode).anchoredPosition);
            Assert.IsTrue(copyNode.gameObject.activeSelf, "复制出来的节点不在记录里，不能被设 inactive");
            Assert.IsFalse(p.transform.Find("panel_e1/button_e2").gameObject.activeSelf);
        }

        [Test]
        public void Update_PrefabMoved_RecordFollows()
        {
            AssetDatabase.CreateFolder(TestPaths.OutDir, "Moved");
            var moved = TestPaths.OutDir + "/Moved/CharacterPanel.prefab";
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath, moved));
            var (_, report) = Build(null, moved);
            Assert.IsEmpty(report.created);
            Assert.IsTrue(File.Exists(UiRecordStore.RecordPathFor(moved)));
            Assert.IsFalse(File.Exists(RecordPath));
        }

        [Test]
        public void Update_NoRecord_ThrowsAndWritesNothing()
        {
            AssetDatabase.DeleteAsset(RecordPath);
            var bytes = File.ReadAllBytes(PrefabPath);
            var ex = Assert.Throws<UiImportException>(() => Build());
            StringAssert.Contains("找不到它的导入记录", ex.Message);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(PrefabPath));
            Assert.IsFalse(File.Exists(RecordPath));
        }

        [Test]
        public void Update_CorruptRecord_ThrowsAndWritesNothing()
        {
            File.WriteAllText(RecordPath, "{x");
            var bytes = File.ReadAllBytes(PrefabPath);
            var ex = Assert.Throws<UiImportException>(() => Build());
            StringAssert.Contains(RecordPath, ex.Message);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(PrefabPath));
            Assert.AreEqual("{x", File.ReadAllText(RecordPath));
        }

        [Test]
        public void Update_ReadOnlyRecord_ThrowsBeforeSavingPrefab()
        {
            var bytes = File.ReadAllBytes(PrefabPath);
            File.SetAttributes(RecordPath, FileAttributes.ReadOnly);
            try
            {
                var doc = Doc();
                doc.Find("e2").rect.x += 10;
                var ex = Assert.Throws<UiImportException>(() => Build(doc));
                StringAssert.Contains("只读", ex.Message);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(PrefabPath), "记录写不了时 Prefab 不能先存进去");
            }
            finally { File.SetAttributes(RecordPath, FileAttributes.Normal); }
        }

        [Test]
        public void Create_AfterMovingOldPrefab_KeepsOldRecord()
        {
            var old = TestPaths.OutDir + "/Old.prefab";
            var oldGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Assert.AreEqual("", AssetDatabase.MoveAsset(PrefabPath, old));
            var (_, report) = Build();
            Assert.AreEqual(65, report.created.Count, "原路径上是首次导入");
            var newGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            Assert.AreNotEqual(oldGuid, newGuid);
            Assert.AreEqual(newGuid, Record().prefabGuid);
            Assert.IsNotNull(UiRecordStore.FindByGuid(oldGuid, old, out var oldAt));
            Assert.AreEqual(UiRecordStore.RecordPathFor(old), oldAt);
        }
    }
}
