// Unity CLI 入口（需要 com.unity.pipeline）。导入逻辑在 Seedance.ModelImport.Editor 里，这里只做参数和返回值。
// pipeline 0.9 起命令可以分组（`unity command seedance model_import`）；0.8 没有分组，注册成 `seedance_model_import` 等。
//   unity command seedance model_import --request <json>                       → ModelImportReport 的 JSON
//   unity command seedance model_capture --prefab <Assets/...prefab> --out <png> → 写出的 PNG 绝对路径（1280×720）

using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Seedance.ModelImport.Editor
{
    static class SeedanceModelImportCommand
    {
#if SEEDANCE_PIPELINE_GROUPS
        [CliCommand("seedance", "model_import", "Import a rigged FBX output (Tripo / Hunyuan zip or dir) as a character prefab with Animator + controller, and show it in Assets/Product/Scene/3DViewer.unity")]
#else
        [CliCommand("seedance_model_import", "Import a rigged FBX output (Tripo / Hunyuan zip or dir) as a character prefab with Animator + controller, and show it in Assets/Product/Scene/3DViewer.unity", Tags = new[] { "seedance" })]
#endif
        static string Import([CliArg("request", "Absolute path to the import request JSON")] string request)
        {
            if (string.IsNullOrEmpty(request) || !File.Exists(request))
                throw new ModelImportException($"请求文件不存在：{request}");
            var req = JsonUtility.FromJson<ModelImportRequest>(File.ReadAllText(request));
            return JsonUtility.ToJson(SeedanceModelImporter.Run(req), true);
        }

#if SEEDANCE_PIPELINE_GROUPS
        [CliCommand("seedance", "model_capture", "Render an imported character prefab offscreen (first frame of its default animation) to a 1280x720 PNG (does not touch open scenes)")]
#else
        [CliCommand("seedance_model_capture", "Render an imported character prefab offscreen (first frame of its default animation) to a 1280x720 PNG (does not touch open scenes)", Tags = new[] { "seedance" })]
#endif
        static string Capture(
            [CliArg("prefab", "Prefab asset path, e.g. Assets/Characters/Knight/Knight.prefab")] string prefab,
            [CliArg("out", "Absolute path of the PNG to write")] string @out)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (asset == null) throw new ModelImportException($"找不到 Prefab：{prefab}");
            ModelCapture.Capture(asset, 1280, 720, @out);
            return Path.GetFullPath(@out);
        }
    }
}
