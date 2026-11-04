using System.Linq;
using Seedance.UiImport.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    /// <summary>用真实夹具 char-panel 准备 sprites / font，再建或更新 Prefab</summary>
    static class PrefabFixture
    {
        public const string PrefabPath = TestPaths.OutDir + "/CharacterPanel.prefab";

        public static UiLayoutDoc Doc() => UiLayoutParser.Parse(TestPaths.CharPanelJson);

        public static (GameObject prefab, ImportReport report) Build(UiLayoutDoc doc = null, string prefabPath = PrefabPath)
        {
            doc ??= Doc();
            var sprites = SpriteAssetWriter.Write(doc, TestPaths.CharPanelDir, TestPaths.OutDir);
            var font = FontResolver.Resolve(null, TestPaths.RequireCachedFont(), TestPaths.OutDir);
            var report = new ImportReport();
            var prefab = PrefabBuilder.BuildOrUpdate(doc, sprites.byElementId, font, prefabPath, report);
            return (prefab, report);
        }

        /// <summary>PrefabPath 的导入记录（没有返回 null）</summary>
        public static UiRecord Record() =>
            UiRecordStore.FindByGuid(AssetDatabase.AssetPathToGUID(PrefabPath), PrefabPath, out _);

        /// <summary>按记录里的 fileId 找到 Prefab 资源里 elementId 对应的节点（没有返回 null）</summary>
        public static GameObject NodeOf(GameObject prefab, string elementId)
        {
            var el = Record()?.elements.Find(x => x.elementId == elementId);
            if (el == null) return null;
            return PrefabNodeIds.Map(prefab, prefab).TryGetValue(el.fileId, out var go) ? go : null;
        }

        public static GameObject Node(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).First(t => t.name == name).gameObject;

        public static RectTransform Rt(GameObject root, string name) => (RectTransform)Node(root, name).transform;

        public static TextMeshProUGUI Text(GameObject root, string name) => Node(root, name).GetComponent<TextMeshProUGUI>();

        /// <summary>在 Prefab 内容上改点东西再存回去，模拟程序手改</summary>
        public static void EditPrefab(System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
