// 建 / 更新角色材质（URP Lit），放在 <importDir>/Materials/。一个材质槽一份：单槽 <P>.mat，多槽 <P>_<槽名>.mat（都用同一组贴图）。
// 首次新建；之后只把贴图槽指向本次的贴图，颜色、光滑度等程序调过的参数不碰。
// 本次没有某个用途的贴图时：槽位里还是上次导入写的那张（GUID 对得上记录）就清空，程序自己换上的不碰。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    public static class MaterialBuilder
    {
        const string LitShader = "Universal Render Pipeline/Lit";
        internal static readonly Func<string, Shader> DefaultFindShader = Shader.Find;
        /// <summary>测试替换用</summary>
        internal static Func<string, Shader> FindShader = DefaultFindShader;

        static readonly (TextureRole role, string prop, string keyword)[] Slots =
        {
            (TextureRole.BaseColor, "_BaseMap", null),
            (TextureRole.Normal, "_BumpMap", "_NORMALMAP"),
            (TextureRole.Metallic, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP"),
        };

        public static Dictionary<string, Material> BuildOrUpdate(string importDir, string prefabName, IReadOnlyList<string> slots,
            IReadOnlyDictionary<TextureRole, Texture2D> byRole, IReadOnlyDictionary<TextureRole, string> recordedGuids, List<string> warnings)
        {
            var shader = FindShader(LitShader);
            if (shader == null)
                throw new ModelImportException("工程没有装 URP（找不到 Universal Render Pipeline/Lit），角色材质建不了。先装 com.unity.render-pipelines.universal 再导入");

            var dir = importDir + "/Materials";
            Directory.CreateDirectory(dir);
            if (slots.Count > 1)
                warnings.Add($"FBX 有 {slots.Count} 个材质槽（{string.Join("、", slots)}），都用了同一组贴图；需要的话手动调整");

            var result = new Dictionary<string, Material>();
            foreach (var slot in slots)
            {
                var path = slots.Count == 1 ? $"{dir}/{prefabName}.mat" : $"{dir}/{prefabName}_{Sanitize(slot)}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                    Apply(mat, byRole, recordedGuids);
                    // 金属度图的 alpha 就是光滑度，URP 会再乘 _Smoothness；首次设 1 让贴图说了算
                    if (byRole.ContainsKey(TextureRole.Metallic)) mat.SetFloat("_Smoothness", 1f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    Apply(mat, byRole, recordedGuids);
                    EditorUtility.SetDirty(mat);
                }
                result[slot] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static void Apply(Material mat, IReadOnlyDictionary<TextureRole, Texture2D> byRole, IReadOnlyDictionary<TextureRole, string> recordedGuids)
        {
            foreach (var (role, prop, keyword) in Slots)
            {
                if (byRole.TryGetValue(role, out var tex) && tex != null)
                {
                    mat.SetTexture(prop, tex);
                    if (keyword != null) mat.EnableKeyword(keyword);
                    continue;
                }
                var current = mat.GetTexture(prop);
                if (current == null || recordedGuids == null || !recordedGuids.TryGetValue(role, out var recorded)) continue;
                if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(current)) != recorded) continue; // 程序换上的，不碰
                mat.SetTexture(prop, null);
                if (keyword != null) mat.DisableKeyword(keyword);
            }
        }

        static string Sanitize(string slot)
        {
            var chars = slot.Select(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') ? c : '_').ToArray();
            return new string(chars);
        }
    }
}
