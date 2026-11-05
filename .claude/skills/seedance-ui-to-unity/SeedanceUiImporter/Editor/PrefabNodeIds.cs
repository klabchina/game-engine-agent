// Prefab 里每个 GameObject 的 local fileID 在资源生命周期内不变（改名、挪层级都不变；复制出来的是新 id），
// 导入记录靠它认节点。LoadPrefabContents 的结果 / 刚保存用的临时根是没有 fileID 的副本，按 sibling-index 路径和资源一一对上。

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public static class PrefabNodeIds
    {
        /// <summary>
        /// assetRoot：已保存的 Prefab 资源根；cloneRoot：结构完全相同的副本。
        /// 返回 资源 GameObject 的 local fileID → 副本里对应的 GameObject。
        /// </summary>
        public static Dictionary<long, GameObject> Map(GameObject assetRoot, GameObject cloneRoot)
        {
            var map = new Dictionary<long, GameObject>();
            Walk(assetRoot.transform, cloneRoot.transform, map);
            return map;
        }

        static void Walk(Transform asset, Transform clone, Dictionary<long, GameObject> map)
        {
            if (asset.childCount != clone.childCount)
                throw new InvalidOperationException($"Prefab 资源和副本结构不一致：{asset.name} 有 {asset.childCount} 个子节点，副本 {clone.name} 有 {clone.childCount} 个");
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset.gameObject, out _, out long id) && id != 0)
                map[id] = clone.gameObject;
            for (var i = 0; i < asset.childCount; i++) Walk(asset.GetChild(i), clone.GetChild(i), map);
        }
    }
}
