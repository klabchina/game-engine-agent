using System.IO;
using UnityEngine;

namespace Seedance.UiImport.Tests
{
    static class TestPaths
    {
        public static string Fixtures => Path.GetFullPath(Path.Combine(Application.dataPath, "../TestFixtures"));
        public static string CharPanelDir => Path.Combine(Fixtures, "char-panel");
        public static string CharPanelZip => Path.Combine(Fixtures, "char-panel.zip");
        public static string CharPanelJson => File.ReadAllText(Path.Combine(CharPanelDir, "layout.json"));
        /// <summary>fetch_font.sh 下载的默认字体；不存在的测试会 Ignore</summary>
        public static string CachedFont => Path.Combine(System.Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
            ?? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), ".cache"),
            "seedance-ui-to-unity/NotoSansCJKsc-Regular.otf");
        public static string RequireCachedFont()
        {
            if (!File.Exists(CachedFont)) NUnit.Framework.Assert.Ignore("先跑 .claude/skills/seedance-ui-to-unity/scripts/fetch_font.sh");
            return CachedFont;
        }
        /// <summary>测试产出的资源都放这里，各测试 TearDown 时删掉</summary>
        public const string OutDir = "Assets/_SeedanceUiTestOut";
    }
}
