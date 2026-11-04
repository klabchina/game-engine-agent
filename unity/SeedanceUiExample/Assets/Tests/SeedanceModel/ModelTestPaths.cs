using System.IO;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    static class ModelTestPaths
    {
        public static string Fixtures => Path.GetFullPath(Path.Combine(Application.dataPath, "../TestFixtures"));
        /// <summary>Tripo 产物（解开的目录）：rig-2026-11-03-m1ctb3.fbx + Textures/（BaseColor / Normal / Metallic）</summary>
        public static string TripoDir => Path.Combine(Fixtures, "rig-tripo");
        /// <summary>同内容的 zip，根目录直接是文件</summary>
        public static string TripoZip => Path.Combine(Fixtures, "rig-tripo.zip");
        public const string TripoFbxName = "rig-2026-11-03-m1ctb3.fbx";
        /// <summary>测试产出的资源都放这里，各测试 TearDown 时删掉</summary>
        public const string OutDir = "Assets/_SeedanceModelTestOut";
        /// <summary>OutDir 下 Prefab 的导入记录所在目录（Assets/SeedanceModel/ 镜像），各测试 TearDown 时一并删掉</summary>
        public const string RecordOutDir = "Assets/SeedanceModel/_SeedanceModelTestOut";
    }
}
