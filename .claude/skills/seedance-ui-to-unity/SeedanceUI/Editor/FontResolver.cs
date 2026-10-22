// 文字元素用的 TMP 字体：传了现成的 TMP Font Asset 就用它；
// 否则把字体文件拷进 <importDir>/Fonts/，生成一份动态 TMP 字体（已生成过就复用）。

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Seedance.UiImport.Editor
{
    public static class FontResolver
    {
        public static TMP_FontAsset Resolve(string tmpFontAssetPath, string fontFilePath, string importDir)
        {
            if (!string.IsNullOrEmpty(tmpFontAssetPath))
            {
                var given = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpFontAssetPath);
                if (given == null) throw new UiImportException($"找不到 TMP 字体资产：{tmpFontAssetPath}");
                return given;
            }
            if (string.IsNullOrEmpty(fontFilePath)) throw new UiImportException("有文字元素，但既没给 TMP 字体也没给字体文件");
            if (!File.Exists(fontFilePath)) throw new UiImportException($"字体文件不存在：{fontFilePath}");

            var fontsDir = importDir.TrimEnd('/') + "/Fonts";
            var assetPath = $"{fontsDir}/{UiNaming.FontAssetName(fontFilePath)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;
            // 同一个字体文件，别的导入目录已经生成过就共用，免得每套 UI 各拷一份十几 MB 的字体、各有一张动态 atlas
            var shared = FindGenerated(Path.GetFileNameWithoutExtension(assetPath));
            if (shared != null) return shared;
            // 生成字体资产要读 TMP Settings
            if (!TmpEssentialsReady())
                throw new UiImportException("工程还没导入 TMP Essential Resources，先跑 unity command seedance ui_prepare 并等 Editor 导入完成");

            Directory.CreateDirectory(fontsDir);
            var fontDst = $"{fontsDir}/{UiNaming.ToAscii(Path.GetFileNameWithoutExtension(fontFilePath))}{Path.GetExtension(fontFilePath)}";
            if (!File.Exists(fontDst)) File.Copy(fontFilePath, fontDst);
            AssetDatabase.ImportAsset(fontDst);
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontDst);
            if (font == null) throw new UiImportException($"Unity 没能把字体文件导入成 Font：{fontDst}");

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            // atlas 贴图和材质必须挂成子资源，否则重开 Editor 后字体丢贴图、文字消失
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static TMP_FontAsset FindGenerated(string assetName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{assetName} t:TMP_FontAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == assetName && path.Contains("/Fonts/"))
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            return null;
        }

        /// <summary>工程里已经有 TMP Essential Resources（TMP Settings、默认 shader）</summary>
        public static bool TmpEssentialsReady() => Resources.Load<TMP_Settings>("TMP Settings") != null;

        /// <summary>
        /// 开始导入 TMP Essential Resources。AssetDatabase.ImportPackage 是延迟执行的——调用返回后 Editor 才真正导入，
        /// 所以这一步单独成一个 CLI 命令（unity command seedance ui_prepare），等 Editor 导完再跑导入。
        /// </summary>
        public static void StartTmpEssentialsImport()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
            if (pkg == null) throw new UiImportException("找不到 TextMeshPro 所在的包（com.unity.ugui）");
            var unitypackage = Path.Combine(pkg.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(unitypackage)) throw new UiImportException($"找不到 TMP Essential Resources：{unitypackage}");
            AssetDatabase.ImportPackage(unitypackage, false);
        }
    }
}
