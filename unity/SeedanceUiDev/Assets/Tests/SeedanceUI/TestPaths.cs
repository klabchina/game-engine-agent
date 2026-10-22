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
        /// <summary>测试产出的资源都放这里，各测试 TearDown 时删掉</summary>
        public const string OutDir = "Assets/_SeedanceUiTestOut";
    }
}
