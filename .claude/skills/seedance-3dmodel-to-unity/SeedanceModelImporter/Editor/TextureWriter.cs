// 把产物贴图写进 <importDir>/Textures/，按用途改成固定名 <P>_<BaseColor|Normal|Metallic>.<ext>。
// Tripo 的贴图名里带每次生成都会变的 UUID，按原名对应的话每次重导都会多一套；按用途固定名就能原地覆盖、GUID 不变。
//
// 金属度贴图：Tripo 给的是 glTF 的 ORM 打包图（实测 R 恒为 255 = AO、G = 粗糙度、B = 金属度，PNG 无 alpha），
// URP Lit 的 _MetallicGlossMap 要 R = 金属度、A = 光滑度。没有 alpha 的金属度图一律按 ORM 转成 R = B、A = 1 - G 的 PNG；
// 带 alpha 的当作已经是 URP 格式，原样拷。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public class TextureWriteResult
    {
        public Dictionary<TextureRole, Texture2D> byRole = new Dictionary<TextureRole, Texture2D>();
        /// <summary>上次导入有、这次没有的用途对应的贴图路径（没删）</summary>
        public List<string> orphanTextures = new List<string>();
        public List<string> warnings = new List<string>();
    }

    public static class TextureWriter
    {
        public static TextureWriteResult Write(IReadOnlyList<string> sourcePaths, string importDir, string prefabName,
            IReadOnlyDictionary<TextureRole, string> previousGuids)
        {
            var result = new TextureWriteResult();
            var texDir = importDir + "/Textures";
            Directory.CreateDirectory(texDir);
            var roles = TextureRoles.Classify(sourcePaths.Select(Path.GetFileName).ToList());

            foreach (var src in sourcePaths)
            {
                var name = Path.GetFileName(src);
                var role = roles[name];
                if (role == TextureRole.Unknown)
                {
                    var keep = $"{texDir}/{name}";
                    File.Copy(src, keep, true);
                    AssetDatabase.ImportAsset(keep, ImportAssetOptions.ForceUpdate);
                    result.warnings.Add($"认不出用途的贴图，已按原名拷入、没连到材质：{name}");
                    continue;
                }

                var ext = role == TextureRole.Metallic ? ".png" : Path.GetExtension(src).ToLowerInvariant();
                var dest = $"{texDir}/{prefabName}_{role}{ext}";
                var prev = PreviousPath(previousGuids, role);
                if (!string.IsNullOrEmpty(prev) && prev != dest) AssetDatabase.DeleteAsset(prev);

                if (role == TextureRole.Metallic) WriteMetallic(src, dest);
                else File.Copy(src, dest, true);
                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
                Configure(dest, role);
                result.byRole[role] = AssetDatabase.LoadAssetAtPath<Texture2D>(dest);
            }

            foreach (var role in TextureRoles.Known)
            {
                if (result.byRole.ContainsKey(role)) continue;
                var prev = PreviousPath(previousGuids, role);
                if (!string.IsNullOrEmpty(prev)) result.orphanTextures.Add(prev);
            }
            return result;
        }

        static string PreviousPath(IReadOnlyDictionary<TextureRole, string> guids, TextureRole role)
        {
            if (guids == null || !guids.TryGetValue(role, out var guid) || string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }

        static void Configure(string path, TextureRole role)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var changed = false;
            if (role == TextureRole.Normal && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (role == TextureRole.Metallic && imp.sRGBTexture)
            {
                imp.sRGBTexture = false;
                changed = true;
            }
            // 产物贴图多是 2K / 4K，默认上限 2048 会把 4K 底色压掉一半
            if (imp.maxTextureSize < 4096)
            {
                imp.maxTextureSize = 4096;
                changed = true;
            }
            if (changed) imp.SaveAndReimport();
        }

        /// <summary>PNG 的 IHDR 颜色类型：4 = 灰度 + alpha，6 = RGBA</summary>
        static bool PngHasAlpha(byte[] bytes) =>
            bytes.Length > 25 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && (bytes[25] == 4 || bytes[25] == 6);

        static void WriteMetallic(string src, string dest)
        {
            var bytes = File.ReadAllBytes(src);
            if (PngHasAlpha(bytes))
            {
                File.WriteAllBytes(dest, bytes);
                return;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!tex.LoadImage(bytes)) throw new ModelImportException($"金属度贴图读不了：{Path.GetFileName(src)}");
                var px = tex.GetPixels32();
                for (var i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    px[i] = new Color32(p.b, 0, 0, (byte)(255 - p.g));
                }
                var outTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
                try
                {
                    outTex.SetPixels32(px);
                    File.WriteAllBytes(dest, outTex.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(outTex); }
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
