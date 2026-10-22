// 一次导入的完整流程：读产物 → 切图成 Sprite → 字体 → 建 / 更新 Prefab → 放进展示场景。

using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Seedance.UiImport.Editor
{
    public static class SeedanceUiImporter
    {
        internal static readonly Func<bool> DefaultIsPlaying = () => EditorApplication.isPlayingOrWillChangePlaymode;
        /// <summary>测试替换用</summary>
        internal static Func<bool> IsPlaying = DefaultIsPlaying;

        public static ImportReport Run(ImportRequest req)
        {
            // Play Mode 下改 Prefab / 打开场景会失败或被还原，而切图已经写进去了——一开始就拒绝
            if (IsPlaying()) throw new UiImportException("Editor 正处于 Play Mode，先退出 Play Mode 再导入");
            var importDir = (req.importDir ?? "").Replace('\\', '/').TrimEnd('/');
            if (!importDir.StartsWith("Assets/"))
                throw new UiImportException($"导入目录必须在 Assets/ 下：{req.importDir}");
            var prefabName = string.IsNullOrEmpty(req.prefabName) ? importDir.Substring(importDir.LastIndexOf('/') + 1) : req.prefabName;
            if (!UiNaming.IsValidPrefabName(prefabName))
                throw new UiImportException($"Prefab 名只能用英文字母、数字、_、-：{prefabName}" +
                                            (string.IsNullOrEmpty(req.prefabName) ? "（取自导入目录名，可以另传 prefabName）" : ""));

            using var source = UiSource.Open(req.source);
            var doc = UiLayoutParser.Parse(File.ReadAllText(source.LayoutJsonPath));
            var report = new ImportReport();
            Directory.CreateDirectory(importDir);

            var sprites = SpriteAssetWriter.Write(doc, source.RootDir, importDir);
            report.orphanSprites.AddRange(sprites.orphanSprites);
            report.warnings.AddRange(sprites.warnings);

            TMPro.TMP_FontAsset font = null;
            if (doc.elements.Any(e => e.IsText))
            {
                if (!FontResolver.TmpEssentialsReady())
                    throw new UiImportException("工程还没导入 TMP Essential Resources，先跑 unity command seedance ui_prepare 并等 Editor 导入完成");
                font = FontResolver.Resolve(req.tmpFont, req.fontFile, importDir);
            }

            var prefab = PrefabBuilder.BuildOrUpdate(doc, sprites.byElementId, font, $"{importDir}/{prefabName}.prefab", report);
            ShowcaseScene.Show(prefab, doc.resolution.width, doc.resolution.height, report);
            AssetDatabase.SaveAssets();
            return report;
        }
    }
}
