// 把产物 FBX 拷进工程并设好 ModelImporter：Generic 骨骼、按 take 切动画、原地播、材质 remap。
//
// 两份样例（Tripo / 混元）实测：Y 轴朝上、fileScale = 0.01（Unity 默认按文件单位换算就对）、角色站立、脚底 y≈0；
// Bake Axis Conversion 不影响高度，但会改变面朝方向（混元开了以后转了 180°）——下面的朝向纠正是按开着它算的。take 名是 "Armature|<clip>"，24fps。
// 两家 FBX 都多带一个顶层 Icosphere（非蒙皮、半径约 1 m、在原点），是转换流程的残留：
// 这里把它认出来（strayMeshes），Prefab 里设 inactive，不参与高度和材质 remap。
//
// 朝向：两家朝向不一样（按本文件的导入设置实测：Tripo 面朝 +X、混元面朝 -Z），统一的轴向设置修不了。按左右成对骨骼（L_/R_、Left/Right、
// _L/_R、.L/.R）算出面朝方向，吸附到 90° 的倍数，给出把它转到 +Z 要绕 Y 转的角度（facingYaw），由 Prefab 的 Model 子节点去转。
//
// 原地播：motionNodeName 设为蒙皮根骨骼；水平位移与朝向不 bake（提取成根运动，Animator 的 Apply Root Motion 关着 → 丢弃），
// 竖直位移 bake 进姿势。重导时程序改过的 clip 设置（循环、根运动、事件、mask、曲线…）按名字整份保留，只更新帧范围。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public class FbxWriteResult
    {
        public string fbxPath;
        /// <summary>本次 FBX 里的动画名（去掉 "Armature|" 前缀），按 take 顺序</summary>
        public List<string> clipNames = new List<string>();
        /// <summary>蒙皮骨骼名，排序去重</summary>
        public List<string> bones = new List<string>();
        public float heightMeters;
        /// <summary>把模型转到面朝 +Z 要绕 Y 轴转的角度（-90 / 0 / 90 / 180）；认不出朝向时为 0</summary>
        public float facingYaw;
        /// <summary>转换流程残留的多余网格（相对模型根的路径），Prefab 里要关掉</summary>
        public List<string> strayMeshes = new List<string>();
        public List<string> warnings = new List<string>();
    }

    public static class FbxWriter
    {
        static readonly string[] LoopKeywords = { "idle", "walk", "run" };

        public static FbxWriteResult Write(string sourceFbx, string importDir, string prefabName, bool firstImport)
        {
            Directory.CreateDirectory(importDir);
            var path = $"{importDir}/{prefabName}.fbx";
            File.Copy(sourceFbx, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.bakeAxisConversion = true;
            imp.importAnimation = true;
            if (firstImport || string.IsNullOrEmpty(imp.motionNodeName))
                imp.motionNodeName = RootBonePath(path);
            imp.SaveAndReimport();

            var result = new FbxWriteResult { fbxPath = path };
            imp.clipAnimations = BuildClips(imp, firstImport, result.clipNames);
            imp.SaveAndReimport();

            Inspect(path, result);
            return result;
        }

        static string ClipName(string takeName)
        {
            var i = takeName.LastIndexOf('|');
            return i >= 0 && i < takeName.Length - 1 ? takeName.Substring(i + 1) : takeName;
        }

        static bool ShouldLoop(string clipName) =>
            LoopKeywords.Any(k => clipName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

        static ModelImporterClipAnimation[] BuildClips(ModelImporter imp, bool firstImport, List<string> names)
        {
            var existing = firstImport
                ? new Dictionary<string, ModelImporterClipAnimation>()
                : imp.clipAnimations.GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in imp.importedTakeInfos)
            {
                var name = ClipName(take.name);
                var first = take.startTime * take.sampleRate;
                var last = take.stopTime * take.sampleRate;
                if (existing.TryGetValue(name, out var keep))
                {
                    keep.takeName = take.name;
                    keep.firstFrame = first;
                    keep.lastFrame = last;
                    clips.Add(keep);
                }
                else
                {
                    clips.Add(new ModelImporterClipAnimation
                    {
                        name = name,
                        takeName = take.name,
                        firstFrame = first,
                        lastFrame = last,
                        loopTime = ShouldLoop(name),
                        lockRootRotation = false,
                        lockRootPositionXZ = false,
                        lockRootHeightY = true,
                        keepOriginalOrientation = true,
                        keepOriginalPositionXZ = true,
                        keepOriginalPositionY = true,
                    });
                }
                names.Add(name);
            }
            return clips.ToArray();
        }

        /// <summary>蒙皮根骨骼相对模型根的路径，如 "Armature/Root"；没有蒙皮网格返回空串</summary>
        static string RootBonePath(string fbxPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var smr = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.rootBone != null);
            return smr == null ? "" : AnimationUtility.CalculateTransformPath(smr.rootBone, model.transform);
        }

        /// <summary>模型根的直接子物体里、非蒙皮的 Icosphere*（模型里有蒙皮网格时）</summary>
        static List<string> FindStray(GameObject root)
        {
            if (!root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any()) return new List<string>();
            return root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("Icosphere", StringComparison.OrdinalIgnoreCase)
                            && t.GetComponent<MeshRenderer>() != null && t.GetComponent<SkinnedMeshRenderer>() == null)
                .Select(t => t.name)
                .ToList();
        }

        static void Inspect(string path, FbxWriteResult result)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = UnityEngine.Object.Instantiate(asset);
            try
            {
                result.strayMeshes = FindStray(go);
                foreach (var s in result.strayMeshes)
                    result.warnings.Add($"FBX 里有一个多余的网格 {s}（转换流程残留），已在 Prefab 里关掉；需要的话手动打开");
                var stray = new HashSet<Transform>(result.strayMeshes.Select(s => go.transform.Find(s)));

                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                result.bones = smrs.SelectMany(s => s.bones).Where(b => b != null).Select(b => b.name)
                    .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

                Bounds? bounds = null;
                void Add(Vector3 p)
                {
                    if (bounds == null) bounds = new Bounds(p, Vector3.zero);
                    else { var b = bounds.Value; b.Encapsulate(p); bounds = b; }
                }
                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh == null) continue;
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    foreach (var v in baked.vertices) Add(smr.transform.TransformPoint(v));
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || stray.Contains(mf.transform)) continue;
                    foreach (var v in mf.sharedMesh.vertices) Add(mf.transform.TransformPoint(v));
                }
                result.heightMeters = bounds?.size.y ?? 0f;

                var yaw = FacingYaw(go);
                result.facingYaw = yaw ?? 0f;
                if (yaw == null) result.warnings.Add("认不出模型朝向（找不到左右成对的骨骼），没有转；需要的话手动转 Model 子节点");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// 按左右成对骨骼算模型面朝方向，返回把它转到 +Z 要绕 Y 轴转的角度（吸附到 90° 倍数，范围 (-180, 180]）。
        /// right = Σ(右骨骼 - 左骨骼) 的水平分量，forward = right × up。找不到成对骨骼返回 null。
        /// </summary>
        public static float? FacingYaw(GameObject model)
        {
            var left = new Dictionary<string, Vector3>();
            var right = new Dictionary<string, Vector3>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                var (side, rest) = Side(t.name.ToLowerInvariant());
                if (side == 'L') left[rest] = t.position;
                else if (side == 'R') right[rest] = t.position;
            }
            var sum = Vector3.zero;
            foreach (var kv in left)
                if (right.TryGetValue(kv.Key, out var r)) sum += r - kv.Value;
            sum.y = 0f;
            if (sum.sqrMagnitude < 1e-8f) return null;
            var forward = Vector3.Cross(sum.normalized, Vector3.up);
            var yaw = Mathf.Round(Vector3.SignedAngle(forward, Vector3.forward, Vector3.up) / 90f) * 90f;
            if (yaw <= -180f) yaw += 360f;
            return yaw == 0f ? 0f : yaw; // 不要 -0
        }

        static (char side, string rest) Side(string n)
        {
            if (n.StartsWith("l_")) return ('L', n.Substring(2));
            if (n.StartsWith("r_")) return ('R', n.Substring(2));
            if (n.StartsWith("left")) return ('L', n.Substring(4).TrimStart('_', '.'));
            if (n.StartsWith("right")) return ('R', n.Substring(5).TrimStart('_', '.'));
            if (n.EndsWith("_l") || n.EndsWith(".l")) return ('L', n.Substring(0, n.Length - 2));
            if (n.EndsWith("_r") || n.EndsWith(".r")) return ('R', n.Substring(0, n.Length - 2));
            return ('\0', null);
        }

        /// <summary>FBX 里的材质槽名（Unity remap 认的名字），不含 stray 网格上的</summary>
        public static IReadOnlyList<string> MaterialSlots(string fbxPath, IReadOnlyCollection<string> strayMeshes)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var stray = new HashSet<string>(strayMeshes ?? Array.Empty<string>());
            return model.GetComponentsInChildren<Renderer>(true)
                .Where(r => !stray.Contains(AnimationUtility.CalculateTransformPath(r.transform, model.transform)))
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Select(m => m.name)
                .Distinct()
                .ToList();
        }

        /// <summary>按槽名 remap 到给定材质；不在 bySlot 里的旧 remap（上一份 FBX 的槽名）清掉</summary>
        public static void RemapMaterials(string fbxPath, IReadOnlyDictionary<string, Material> bySlot)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            foreach (var id in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
                if (!bySlot.ContainsKey(id.name)) imp.RemoveRemap(id);
            foreach (var kv in bySlot)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.SaveAndReimport();
        }
    }
}
