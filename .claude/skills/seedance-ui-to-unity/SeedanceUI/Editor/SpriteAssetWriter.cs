// 切图 → <importDir>/Sprites/<type>_<id>.png，导入成 UI Sprite。
// 按固定路径覆盖写入、从不先删，Unity 会保留原 .meta 的 GUID，已有引用不断。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    public static class SpriteAssetWriter
    {
        public static SpriteWriteResult Write(UiLayoutDoc doc, string sourceRoot, string importDir)
        {
            var result = new SpriteWriteResult();
            var spritesDir = importDir.TrimEnd('/') + "/Sprites";
            Directory.CreateDirectory(spritesDir);

            // 目标路径 → 写进去的那张源图（layout.json 的 image），同一个目标只写一次
            var written = new Dictionary<string, string>();
            var elementDst = new Dictionary<string, string>();
            foreach (var e in doc.elements)
            {
                if (e.IsText || e.missing || e.image == null) continue;
                var dst = spritesDir + "/" + UiNaming.SpriteFileName(doc, e);
                elementDst[e.id] = dst;
                if (written.TryGetValue(dst, out var firstImage))
                {
                    if (firstImage != e.image)
                        result.warnings.Add($"{e.id} {e.name}：和同款元素的切图不是同一张（{e.image} / {firstImage}），用了 {firstImage}");
                    continue;
                }
                var src = Path.Combine(sourceRoot, e.image);
                if (!File.Exists(src)) throw new UiImportException($"元素 {e.id} 的切图不存在：{e.image}");
                File.Copy(src, dst, true);
                written[dst] = e.image;
            }

            AssetDatabase.Refresh();
            foreach (var dst in written.Keys) ConfigureImporter(dst);

            var sprites = written.Keys.ToDictionary(p => p, AssetDatabase.LoadAssetAtPath<Sprite>);
            foreach (var kv in elementDst) result.byElementId[kv.Key] = sprites[kv.Value];

            result.orphanSprites = Directory.GetFiles(spritesDir, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !written.ContainsKey(p))
                .OrderBy(p => p)
                .ToList();
            return result;
        }

        static void ConfigureImporter(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.GetSourceTextureWidthAndHeight(out var w, out var h);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            // 默认 2048 会把大切图（面板常超过 2048）缩小
            imp.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(w, h)), 32, 16384);
            imp.SaveAndReimport();
        }
    }
}
