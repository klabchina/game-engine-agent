using System;
using System.IO;
using System.Linq;
using Seedance.ModelImport.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Seedance.ModelImport.Tests
{
    /// <summary>总流程测试共用：请求、清理、构造产物</summary>
    static class ModelImportFixture
    {
        public const string P = "Knight";
        public const string ImportDir = ModelTestPaths.OutDir + "/Knight";
        public const string PrefabPath = ImportDir + "/Knight.prefab";
        public const string ControllerPath = ImportDir + "/Knight.controller";
        public const string FbxPath = ImportDir + "/Knight.fbx";

        public static ModelImportRequest Req(string source = null) => new ModelImportRequest
        {
            source = source ?? ModelTestPaths.TripoZip, importDir = ImportDir, prefabName = "",
        };

        public static void CleanUp()
        {
            SeedanceModelImporter.IsPlaying = SeedanceModelImporter.DefaultIsPlaying;
            AssetDatabase.DeleteAsset(ModelTestPaths.OutDir);
            AssetDatabase.DeleteAsset(ModelTestPaths.RecordOutDir);
            AssetDatabase.DeleteAsset("Assets/Product");
        }

        public static string NewScratch()
        {
            var d = Path.Combine(Path.GetTempPath(), "seedance-model-run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Tripo 的 FBX + 一张无后缀贴图（模拟混元产物的贴图形态）</summary>
        public static string SingleTextureSource(string scratch)
        {
            var dir = Path.Combine(scratch, "single");
            Directory.CreateDirectory(Path.Combine(dir, "Textures"));
            File.Copy(Path.Combine(ModelTestPaths.TripoDir, ModelTestPaths.TripoFbxName), Path.Combine(dir, "hyrig-x.fbx"));
            var t = new Texture2D(8, 8, TextureFormat.RGB24, false);
            File.WriteAllBytes(Path.Combine(dir, "Textures", "texture_20250901.png"), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            return dir;
        }

        public static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        public static AnimatorController Controller => AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        public static ModelRecord Record() =>
            JsonUtility.FromJson<ModelRecord>(File.ReadAllText(ModelRecordStore.RecordPathFor(PrefabPath)));

        public static void WriteRecord(ModelRecord r)
        {
            File.WriteAllText(ModelRecordStore.RecordPathFor(PrefabPath), JsonUtility.ToJson(r, true));
            AssetDatabase.ImportAsset(ModelRecordStore.RecordPathFor(PrefabPath));
        }

        /// <summary>改 Prefab：在 LoadPrefabContents 的副本上动手再存回</summary>
        public static void EditPrefab(Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static Transform FindDeep(Transform t, string name) =>
            t.name == name ? t : t.Cast<Transform>().Select(c => FindDeep(c, name)).FirstOrDefault(x => x != null);
    }
}
