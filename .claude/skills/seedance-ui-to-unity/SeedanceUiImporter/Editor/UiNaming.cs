// 进 Unity 的资源名、节点名一律 ASCII（上游元素名是中文）。中文名存在 SeedanceUiElement.sourceName 里。

using System.IO;
using System.Text.RegularExpressions;

namespace Seedance.UiImport.Editor
{
    public static class UiNaming
    {
        static readonly Regex PrefabName = new Regex("^[A-Za-z0-9_-]+$");
        static readonly Regex NonAscii = new Regex("[^A-Za-z0-9_-]");
        static readonly Regex Underscores = new Regex("_+");

        /// <summary>
        /// 同款元素（sameAs）共用一张图，所以文件名取被引用的那个元素：&lt;type&gt;_&lt;id&gt;.png。
        /// 名字由 id 推出，ui-maker 里改元素名不会让 Sprite 换文件。
        /// </summary>
        public static string SpriteFileName(UiLayoutDoc doc, UiLayoutElement e)
        {
            var master = e.sameAs != null ? doc.Find(e.sameAs) : e;
            return $"{master.type}_{master.id}.png";
        }

        public static string NodeName(UiLayoutElement e) => $"{e.type}_{e.id}";

        public static bool IsValidPrefabName(string s) => !string.IsNullOrEmpty(s) && PrefabName.IsMatch(s);

        public static string FontAssetName(string fontFilePath) =>
            ToAscii(Path.GetFileNameWithoutExtension(fontFilePath)) + "_SDF";

        public static string ToAscii(string s)
        {
            var r = Underscores.Replace(NonAscii.Replace(s ?? "", "_"), "_").Trim('_');
            return r.Length == 0 ? "Font" : r;
        }
    }
}
