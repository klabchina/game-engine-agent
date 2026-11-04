// 一次导入的完整流程：校验 → 读产物 → 贴图 → FBX → 材质 → Controller → Prefab → 记录 →（3DViewer）。
// 首次导入：资源都以 Prefab 名命名、放在 importDir。重导：资源沿用记录里那份 FBX 所在的目录和名字
// （程序把 Prefab 挪 / 改名了也不会另建一套 FBX、材质、Controller）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public static class SeedanceModelImporter
    {
        internal static readonly Func<bool> DefaultIsPlaying = () => EditorApplication.isPlayingOrWillChangePlaymode;
        /// <summary>测试替换用</summary>
        internal static Func<bool> IsPlaying = DefaultIsPlaying;

        static readonly Regex PrefabName = new Regex("^[A-Za-z0-9_-]+$");
        const float MinHeight = 0.1f, MaxHeight = 10f;

        public static ModelImportReport Run(ModelImportRequest req)
        {
            // Play Mode 下改 Prefab / 打开场景会失败或被还原——一开始就拒绝
            if (IsPlaying()) throw new ModelImportException("Editor 正处于 Play Mode，先退出 Play Mode 再导入");
            var importDir = (req.importDir ?? "").Replace('\\', '/').TrimEnd('/');
            if (!importDir.StartsWith("Assets/"))
                throw new ModelImportException($"导入目录必须在 Assets/ 下：{req.importDir}");
            var prefabName = string.IsNullOrEmpty(req.prefabName) ? importDir.Substring(importDir.LastIndexOf('/') + 1) : req.prefabName;
            if (!PrefabName.IsMatch(prefabName))
                throw new ModelImportException($"Prefab 名只能用英文字母、数字、_、-：{prefabName}" +
                                               (string.IsNullOrEmpty(req.prefabName) ? "（取自导入目录名，可以另传 prefabName）" : ""));
            var prefabPath = $"{importDir}/{prefabName}.prefab";

            // 记录读不出来 / 只读 / Prefab 在而记录不在：停下，这时还什么都没写
            ModelRecordStore.ValidateAll();
            ModelRecord record = null;
            string recordPath = null;
            if (File.Exists(prefabPath))
            {
                record = ModelRecordStore.FindByGuid(AssetDatabase.AssetPathToGUID(prefabPath), prefabPath, out recordPath);
                if (record == null)
                    throw new ModelImportException($"{prefabPath} 已存在但找不到它的导入记录（{ModelRecordStore.Root}/ 下）。" +
                                                   "从版本库恢复记录；或删掉这个 Prefab 再导入，会按首次导入重建（程序加的东西会丢）");
            }
            ModelRecordStore.EnsureWritable(prefabPath, recordPath);
            var first = record == null;

            using var source = ModelSource.Open(req.source);
            var report = new ModelImportReport { prefabPath = prefabPath };

            // 资源放哪、叫什么
            var assetDir = importDir;
            var assetName = prefabName;
            var recordedFbx = first ? null : AssetDatabase.GUIDToAssetPath(record.fbxGuid);
            if (!string.IsNullOrEmpty(recordedFbx) && File.Exists(recordedFbx))
            {
                assetDir = Path.GetDirectoryName(recordedFbx).Replace('\\', '/');
                assetName = Path.GetFileNameWithoutExtension(recordedFbx);
            }
            Directory.CreateDirectory(assetDir);

            var prevTex = first
                ? new Dictionary<TextureRole, string>()
                : record.textures.Where(t => Enum.TryParse<TextureRole>(t.role, out _))
                    .ToDictionary(t => (TextureRole)Enum.Parse(typeof(TextureRole), t.role), t => t.guid);
            var tex = TextureWriter.Write(source.TexturePaths, assetDir, assetName, prevTex);
            report.orphanTextures.AddRange(tex.orphanTextures);
            report.warnings.AddRange(tex.warnings);

            var fbx = FbxWriter.Write(source.FbxPath, assetDir, assetName, first);
            report.warnings.AddRange(fbx.warnings);
            report.heightMeters = fbx.heightMeters;
            if (fbx.heightMeters < MinHeight || fbx.heightMeters > MaxHeight)
                report.warnings.Add($"模型高 {fbx.heightMeters:0.##} m，不在 {MinHeight}~{MaxHeight} m 之间，可能缩放不对");

            var slots = FbxWriter.MaterialSlots(fbx.fbxPath, fbx.strayMeshes);
            var mats = MaterialBuilder.BuildOrUpdate(assetDir, assetName, slots, tex.byRole, prevTex, report.warnings);
            FbxWriter.RemapMaterials(fbx.fbxPath, mats);

            var clipsByName = AssetDatabase.LoadAllAssetsAtPath(fbx.fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());
            var clips = fbx.clipNames.Where(clipsByName.ContainsKey).Select(n => clipsByName[n]).ToList();
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx.fbxPath);
            var looped = new HashSet<string>(imp.clipAnimations.Where(c => c.loopTime).Select(c => c.name));
            report.clips.AddRange(fbx.clipNames.Select(n => looped.Contains(n) ? $"{n} (loop)" : n));

            var controllerPath = ExistingControllerPath(prefabPath) ?? $"{assetDir}/{assetName}.controller";
            var controller = ControllerBuilder.BuildOrUpdate(controllerPath, clips, record?.clips, report);
            report.controllerPath = controllerPath;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.fbxPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx.fbxPath).OfType<Avatar>().FirstOrDefault();
            if (first) ModelRecordStore.ReleaseMirrorPath(prefabPath);
            var prefab = ModelPrefabBuilder.BuildOrUpdate(prefabPath, model, controller, avatar, first ? fbx.strayMeshes : null, report.warnings);

            if (record != null)
                report.removedBones.AddRange(record.bones.Except(fbx.bones).OrderBy(b => b, StringComparer.Ordinal));
            var newRecord = new ModelRecord
            {
                prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                fbxGuid = AssetDatabase.AssetPathToGUID(fbx.fbxPath),
                source = GuessSource(source),
                clips = (record?.clips ?? new List<string>()).Union(report.createdStates).ToList(),
                textures = tex.byRole.Select(kv => new RoleGuid
                {
                    role = kv.Key.ToString(), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Value)),
                }).ToList(),
                materials = mats.Values.Select(m => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).Distinct().ToList(),
                bones = fbx.bones,
            };
            try { ModelRecordStore.Save(prefabPath, newRecord, recordPath); }
            catch (Exception ex) when (!(ex is ModelImportException))
            {
                throw new ModelImportException($"Prefab 已保存，但导入记录没写成：{ex.Message}。排除原因后从版本库恢复 Prefab 再导入");
            }

            ViewerScene.Show(prefab, report);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Prefab 里 Animator 已经指向的 Controller（程序可能挪过它）</summary>
        static string ExistingControllerPath(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var ctrl = prefab == null ? null : prefab.GetComponentsInChildren<Animator>(true).Select(a => a.runtimeAnimatorController).FirstOrDefault(c => c != null);
            var path = ctrl == null ? null : AssetDatabase.GetAssetPath(ctrl);
            return string.IsNullOrEmpty(path) || !path.EndsWith(".controller") ? null : path;
        }

        static string GuessSource(ModelSource source)
        {
            var fbxName = Path.GetFileName(source.FbxPath).ToLowerInvariant();
            if (fbxName.StartsWith("hyrig") || fbxName.Contains("hunyuan")) return "hunyuan";
            if (fbxName.Contains("tripo") || source.TexturePaths.Any(t => Path.GetFileName(t).StartsWith("tripo_", StringComparison.OrdinalIgnoreCase))) return "tripo";
            return "unknown";
        }
    }
}
