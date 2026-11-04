using System.IO;
using System.Linq;
using NUnit.Framework;
using Seedance.UiImport.Editor;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    public class PrefabNodeIdsTests
    {
        const string Path_ = TestPaths.OutDir + "/Ids.prefab";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(TestPaths.OutDir);

        /// <summary>Root/A/B</summary>
        static GameObject MakeTemp()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var a = new GameObject("A", typeof(RectTransform));
            var b = new GameObject("B", typeof(RectTransform));
            a.transform.SetParent(root.transform, false);
            b.transform.SetParent(a.transform, false);
            return root;
        }

        static GameObject SaveTemp()
        {
            Directory.CreateDirectory(TestPaths.OutDir);
            var root = MakeTemp();
            var asset = PrefabUtility.SaveAsPrefabAsset(root, Path_);
            Object.DestroyImmediate(root);
            return asset;
        }

        static long IdOf(GameObject asset, GameObject contents, string name)
        {
            var map = PrefabNodeIds.Map(asset, contents);
            return map.Single(kv => kv.Value.name == name).Key;
        }

        [Test]
        public void Map_StableAcrossRenameAndReparent()
        {
            var asset = SaveTemp();
            var contents = PrefabUtility.LoadPrefabContents(Path_);
            var idB = IdOf(asset, contents, "B");
            var b = contents.transform.Find("A/B");
            b.name = "Renamed";
            b.SetParent(contents.transform, false);
            asset = PrefabUtility.SaveAsPrefabAsset(contents, Path_);
            PrefabUtility.UnloadPrefabContents(contents);

            contents = PrefabUtility.LoadPrefabContents(Path_);
            try
            {
                var map = PrefabNodeIds.Map(asset, contents);
                Assert.AreEqual("Renamed", map[idB].name);
                Assert.AreSame(contents.transform, map[idB].transform.parent);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void Map_DuplicatedNodeGetsNewId()
        {
            var asset = SaveTemp();
            var contents = PrefabUtility.LoadPrefabContents(Path_);
            var idB = IdOf(asset, contents, "B");
            var b = contents.transform.Find("A/B");
            var copy = Object.Instantiate(b.gameObject, b.parent);
            copy.name = "B";
            asset = PrefabUtility.SaveAsPrefabAsset(contents, Path_);
            PrefabUtility.UnloadPrefabContents(contents);

            contents = PrefabUtility.LoadPrefabContents(Path_);
            try
            {
                var map = PrefabNodeIds.Map(asset, contents);
                Assert.AreEqual(4, map.Count);
                var bs = map.Where(kv => kv.Value.name == "B").ToList();
                Assert.AreEqual(2, bs.Count);
                Assert.AreNotEqual(bs[0].Key, bs[1].Key);
                Assert.AreEqual(0, map[idB].transform.GetSiblingIndex(), "原来的 id 仍对应原节点");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void Map_NewPrefabFromTempRoot()
        {
            Directory.CreateDirectory(TestPaths.OutDir);
            var root = MakeTemp();
            try
            {
                var asset = PrefabUtility.SaveAsPrefabAsset(root, Path_);
                var map = PrefabNodeIds.Map(asset, root);
                Assert.AreEqual(3, map.Count);
                CollectionAssert.AreEquivalent(new[] { "Root", "A", "B" }, map.Values.Select(g => g.name));
                Assert.IsTrue(map.Values.All(g => g.scene == root.scene), "值是临时根里的对象，不是资源对象");
                CollectionAssert.AllItemsAreUnique(map.Keys);
                Assert.IsTrue(map.Keys.All(k => k != 0));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
