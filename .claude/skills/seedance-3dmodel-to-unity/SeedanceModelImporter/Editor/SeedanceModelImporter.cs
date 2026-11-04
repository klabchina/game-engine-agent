// 一次导入的完整流程：校验 → 读产物 → 贴图 → FBX → 材质 → Controller → Prefab → 记录 →（3DViewer）。
// 首次导入：资源都以 Prefab 名命名、放在 importDir。重导：资源沿用记录里那份 FBX 所在的目录和名字
// （程序把 Prefab 挪 / 改名了也不会另建一套 FBX、材质、Controller）；Controller 认记录里的那份。

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
            if (first)
            {
                // 程序把 Prefab 改名 / 挪走了，却还用旧名导入：同目录的 <P>.fbx 归那个 Prefab 所有，覆盖它会改坏那个 Prefab
                var owner = ModelRecordStore.FindByFbxGuid(AssetDatabase.AssetPathToGUID($"{importDir}/{prefabName}.fbx"));
                var ownerPath = owner == null ? null : AssetDatabase.GUIDToAssetPath(owner.prefabGuid);
                if (!string.IsNullOrEmpty(ownerPath) && File.Exists(ownerPath))
                    throw new ModelImportException($"{importDir}/{prefabName}.fbx 已经是 {ownerPath} 的模型（那个 Prefab 是从这里导入后改名 / 挪走的）。" +
                                                   $"要更新它，就用 prefabName={Path.GetFileNameWithoutExtension(ownerPath)}、导入目录 {Path.GetDirectoryName(ownerPath)?.Replace('\\', '/')} 重导；要另建一个角色，换个导入目录或 Prefab 名。这次什么都还没写");
            }

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
            // 程序在骨骼下加的挂点：要在 FBX 覆盖之前记下来（骨骼没了以后就找不到它原来挂在哪）
            var attachments = first ? new List<(string name, string parent)>() : ModelPrefabBuilder.Attachments(prefabPath);
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

            var recordedController = first || string.IsNullOrEmpty(record.controllerGuid) ? null : AssetDatabase.GUIDToAssetPath(record.controllerGuid);
            var controllerPath = !string.IsNullOrEmpty(recordedController) && File.Exists(recordedController)
                ? recordedController
                : $"{assetDir}/{assetName}.controller";
            var controller = ControllerBuilder.BuildOrUpdate(controllerPath, clips, record?.clips, report);
            report.controllerPath = controllerPath;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx.fbxPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx.fbxPath).OfType<Avatar>().FirstOrDefault();
            if (first) ModelRecordStore.ReleaseMirrorPath(prefabPath);
            var prefab = ModelPrefabBuilder.BuildOrUpdate(prefabPath, model, controller, avatar, first ? fbx.strayMeshes : null,
                fbx.facingYaw, record?.facingYaw, report.warnings);
            // 记录里存“导入设到 Model 子节点上的朝向”：这次没改成（程序转过）就沿用上次的，下次照样认得出程序转过
            var modelNode = prefab.GetComponentsInChildren<Animator>(true).FirstOrDefault()?.transform;
            var appliedYaw = modelNode != null && Mathf.Abs(Mathf.DeltaAngle(modelNode.localEulerAngles.y, fbx.facingYaw)) < 0.5f
                ? fbx.facingYaw
                : record?.facingYaw ?? fbx.facingYaw;

            if (record != null)
            {
                report.removedBones.AddRange(record.bones.Except(fbx.bones).OrderBy(b => b, StringComparer.Ordinal));
                var removed = new HashSet<string>(report.removedBones);
                foreach (var a in attachments.Where(a => removed.Contains(a.parent)))
                    report.warnings.Add($"程序加的 {a.name} 原来挂在骨骼 {a.parent} 下，新模型里没有这根骨骼了，检查它还在不在、位置对不对");
            }
            var newRecord = new ModelRecord
            {
                prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                fbxGuid = AssetDatabase.AssetPathToGUID(fbx.fbxPath),
                controllerGuid = AssetDatabase.AssetPathToGUID(controllerPath),
                source = GuessSource(source),
                clips = (record?.clips ?? new List<string>()).Union(report.createdStates).Union(report.updatedStates).ToList(),
                textures = tex.byRole.Select(kv => new RoleGuid
                {
                    role = kv.Key.ToString(), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kv.Value)),
                }).ToList(),
                materials = mats.Values.Select(m => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))).Distinct().ToList(),
                bones = fbx.bones,
                facingYaw = appliedYaw,
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

        static string GuessSource(ModelSource source)
        {
            var fbxName = Path.GetFileName(source.FbxPath).ToLowerInvariant();
            if (fbxName.StartsWith("hyrig") || fbxName.Contains("hunyuan")) return "hunyuan";
            if (fbxName.Contains("tripo") || source.TexturePaths.Any(t => Path.GetFileName(t).StartsWith("tripo_", StringComparison.OrdinalIgnoreCase))) return "tripo";
            return "unknown";
        }
    }
}
