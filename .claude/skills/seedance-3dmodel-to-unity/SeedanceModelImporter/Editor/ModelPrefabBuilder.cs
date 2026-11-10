// 组装角色 Prefab：
//   <P>            根，空物体（程序挂脚本、碰撞体的地方）
//     └─ <P>_Model FBX 模型的嵌套 Prefab 实例；Animator 挂在这里
// Animator 必须和模型根在同一个物体上：FBX 里动画曲线的路径是相对模型根的（Armature/Root/...），挂在外层根上就绑不上。
// 重导：FBX 原地覆盖后嵌套实例自动更新；这里只确认 Animator 在、指向本次的 Controller / Avatar，其余（程序加的东西）都不碰。
// 朝向：Model 子节点绕 Y 转 facingYaw 让模型面朝 +Z（根不转，留给程序）。重导时 Model 子节点的 yaw 还等于上次导入设的值
// （程序没转过）才跟着改，否则不动、写 warning。

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
        /// <param name="facingYaw">Model 子节点要设的朝向（度）</param>
        /// <param name="previousYaw">上次导入设的朝向；首次导入传 null</param>
        public static GameObject BuildOrUpdate(string prefabPath, GameObject fbxModel, AnimatorController controller, Avatar avatar,
            IReadOnlyList<string> strayToDisable, float facingYaw, float? previousYaw, List<string> warnings)
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
                    model.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                    foreach (var s in strayToDisable ?? new string[0])
                    {
                        var t = model.transform.Find(s);
                        if (t != null) t.gameObject.SetActive(false);
                    }
                    Configure(model, controller, avatar, warnings);
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
                    model.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                    warnings.Add($"Prefab 里找不到 FBX 模型实例（可能被删了），已重新放了一份 {model.name}");
                }
                else
                {
                    var current = model.transform.localEulerAngles.y;
                    var untouched = previousYaw.HasValue && Mathf.Abs(Mathf.DeltaAngle(current, previousYaw.Value)) < 0.5f;
                    if (untouched) model.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                    else if (Mathf.Abs(Mathf.DeltaAngle(current, facingYaw)) >= 0.5f)
                        warnings.Add($"{model.name} 的朝向被程序改过（现在绕 Y {current:0.#}°），这次产物要转 {facingYaw:0.#}° 才面朝 +Z，没改；需要的话手动调整");
                }
                Configure(model, controller, avatar, warnings);
                return PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>程序在模型实例里加的物体（added GameObject）及其父节点名</summary>
        public static List<(string name, string parent)> Attachments(string prefabPath)
        {
            var result = new List<(string, string)>();
            if (!File.Exists(prefabPath)) return result;
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var t in contents.GetComponentsInChildren<Transform>(true))
                    if (t.parent != null && PrefabUtility.IsAddedGameObjectOverride(t.gameObject))
                        result.Add((t.name, t.parent.name));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return result;
        }

        /// <summary>挂在这些骨骼下的程序加的物体，格式 "Weapon（Spine01）"</summary>
        public static List<string> AttachmentsUnder(string prefabPath, ISet<string> bones) =>
            Attachments(prefabPath).Where(a => bones.Contains(a.parent)).Select(a => $"{a.name}（{a.parent}）").ToList();

        static void Configure(GameObject model, AnimatorController controller, Avatar avatar, List<string> warnings)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
                animator.applyRootMotion = false; // 原地播：动画里的水平位移 / 转向提取成根运动后丢掉，由程序控制移动
            }
            var current = animator.runtimeAnimatorController;
            if (current == null || current == controller) animator.runtimeAnimatorController = controller;
            else
                // 程序换上了自己的 / 共用的 Controller 或 AnimatorOverrideController：不换回去
                warnings.Add($"Animator 上挂的是程序换的 Controller（{AssetDatabase.GetAssetPath(current)}），没改它；" +
                             $"本次的动画更新在 {AssetDatabase.GetAssetPath(controller)} 里，需要的话手动同步");
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
