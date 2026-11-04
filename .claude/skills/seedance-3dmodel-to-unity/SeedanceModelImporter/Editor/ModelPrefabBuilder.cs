// 组装角色 Prefab：
//   <P>            根，空物体（程序挂脚本、碰撞体的地方）
//     └─ <P>_Model FBX 模型的嵌套 Prefab 实例；Animator 挂在这里
// Animator 必须和模型根在同一个物体上：FBX 里动画曲线的路径是相对模型根的（Armature/Root/...），挂在外层根上就绑不上。
// 重导：FBX 原地覆盖后嵌套实例自动更新；这里只确认 Animator 在、指向本次的 Controller / Avatar，其余（程序加的东西）都不碰。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public static class ModelPrefabBuilder
    {
        /// <param name="strayToDisable">首次导入时要关掉的残留网格（相对模型根的路径）；重导传空</param>
        public static GameObject BuildOrUpdate(string prefabPath, GameObject fbxModel, AnimatorController controller, Avatar avatar,
            IReadOnlyList<string> strayToDisable, List<string> warnings)
        {
            var name = Path.GetFileNameWithoutExtension(prefabPath);
            if (!File.Exists(prefabPath))
            {
                var root = new GameObject(name);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
                    model.name = name + "_Model";
                    model.transform.SetParent(root.transform, false);
                    foreach (var s in strayToDisable ?? new string[0])
                    {
                        var t = model.transform.Find(s);
                        if (t != null) t.gameObject.SetActive(false);
                    }
                    Configure(model, controller, avatar);
                    return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var model = FindModel(contents, fbxModel);
                if (model == null)
                {
                    model = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
                    model.name = name + "_Model";
                    model.transform.SetParent(contents.transform, false);
                    warnings.Add($"Prefab 里找不到 FBX 模型实例（可能被删了），已重新放了一份 {model.name}");
                }
                Configure(model, controller, avatar);
                return PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        static void Configure(GameObject model, AnimatorController controller, Avatar avatar)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
                animator.applyRootMotion = false; // 原地播：动画里的水平位移 / 转向提取成根运动后丢掉，由程序控制移动
            }
            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
        }

        /// <summary>contents 里来自这个 FBX 的最外层嵌套实例根</summary>
        static GameObject FindModel(GameObject contents, GameObject fbxModel)
        {
            var fbxPath = AssetDatabase.GetAssetPath(fbxModel);
            return contents.GetComponentsInChildren<Transform>(true)
                .Select(t => t.gameObject)
                .FirstOrDefault(g => PrefabUtility.IsAnyPrefabInstanceRoot(g)
                                     && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(g)) == fbxPath);
        }
    }
}
