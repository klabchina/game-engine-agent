// 按文件名认贴图用途（不分大小写）：basecolor / albedo / diffuse → 底色，normal → 法线，metallic → 金属度。
// 只有一张且认不出的当底色（混元产物就是一张 texture_xxx.png）。同一用途多张时只认第一张，其余算认不出。

using System;
using System.Collections.Generic;
using System.Linq;

namespace Seedance.ModelImport.Editor
{
    public enum TextureRole { Unknown, BaseColor, Normal, Metallic }

    public static class TextureRoles
    {
        static TextureRole Guess(string fileName)
        {
            var n = fileName.ToLowerInvariant();
            if (n.Contains("basecolor") || n.Contains("albedo") || n.Contains("diffuse")) return TextureRole.BaseColor;
            if (n.Contains("normal")) return TextureRole.Normal;
            if (n.Contains("metallic")) return TextureRole.Metallic;
            return TextureRole.Unknown;
        }

        /// <summary>文件名 → 用途。传入顺序决定同一用途多张时认哪张</summary>
        public static Dictionary<string, TextureRole> Classify(IReadOnlyList<string> fileNames)
        {
            var result = new Dictionary<string, TextureRole>(StringComparer.Ordinal);
            if (fileNames.Count == 1)
            {
                var g = Guess(fileNames[0]);
                result[fileNames[0]] = g == TextureRole.Unknown ? TextureRole.BaseColor : g;
                return result;
            }
            var taken = new HashSet<TextureRole>();
            foreach (var name in fileNames)
            {
                var g = Guess(name);
                result[name] = g != TextureRole.Unknown && taken.Add(g) ? g : TextureRole.Unknown;
            }
            return result;
        }

        public static IEnumerable<TextureRole> Known => Enum.GetValues(typeof(TextureRole)).Cast<TextureRole>().Where(r => r != TextureRole.Unknown);
    }
}
