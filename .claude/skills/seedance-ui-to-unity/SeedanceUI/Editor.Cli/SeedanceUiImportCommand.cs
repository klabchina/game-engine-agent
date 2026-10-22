// Unity CLI 入口（需要 com.unity.pipeline）。导入逻辑在 Seedance.UiImport.Editor 里，这里只做参数和返回值。
// pipeline 0.9 起命令可以分组（`unity command seedance ui_import`）；0.8 没有分组，注册成 `seedance_ui_import` 等。
//   unity command seedance ui_prepare                                       → "ready" 或 "importing"
//   unity command seedance ui_import --request <json>                       → ImportReport 的 JSON
//   unity command seedance ui_capture --prefab <Assets/...prefab> --out <png> → 写出的 PNG 绝对路径

using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    static class SeedanceUiImportCommand
    {
#if SEEDANCE_PIPELINE_GROUPS
        [CliCommand("seedance", "ui_prepare",
            "Make sure TMP Essential Resources are imported. Returns 'ready', or 'importing' (wait for the Editor to finish importing, then call again)")]
#else
        [CliCommand("seedance_ui_prepare",
            "Make sure TMP Essential Resources are imported. Returns 'ready', or 'importing' (wait for the Editor to finish importing, then call again)", Tags = new[] { "seedance" })]
#endif
        static string Prepare() => FontResolver.PrepareTmpEssentials();

#if SEEDANCE_PIPELINE_GROUPS
        [CliCommand("seedance", "ui_import", "Import a Seedance ui-maker output (out/ dir or ui.zip) as a UGUI prefab and show it in Assets/Product/Scene/UIShowcase.unity")]
#else
        [CliCommand("seedance_ui_import", "Import a Seedance ui-maker output (out/ dir or ui.zip) as a UGUI prefab and show it in Assets/Product/Scene/UIShowcase.unity", Tags = new[] { "seedance" })]
#endif
        static string Import([CliArg("request", "Absolute path to the import request JSON")] string request)
        {
            if (string.IsNullOrEmpty(request) || !File.Exists(request))
                throw new UiImportException($"请求文件不存在：{request}");
            var req = JsonUtility.FromJson<ImportRequest>(File.ReadAllText(request));
            return JsonUtility.ToJson(SeedanceUiImporter.Run(req), true);
        }

#if SEEDANCE_PIPELINE_GROUPS
        [CliCommand("seedance", "ui_capture", "Render an imported Seedance UI prefab offscreen at its design resolution to a PNG (does not touch open scenes)")]
#else
        [CliCommand("seedance_ui_capture", "Render an imported Seedance UI prefab offscreen at its design resolution to a PNG (does not touch open scenes)", Tags = new[] { "seedance" })]
#endif
        static string Capture(
            [CliArg("prefab", "Prefab asset path, e.g. Assets/UI/Generated/X/X.prefab")] string prefab,
            [CliArg("out", "Absolute path of the PNG to write")] string @out)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            if (asset == null) throw new UiImportException($"找不到 Prefab：{prefab}");
            var root = asset.GetComponent<SeedanceUiRoot>();
            if (root == null) throw new UiImportException($"{prefab} 不是 Seedance UI 导入的 Prefab（根上没有 SeedanceUiRoot）");
            PrefabCapture.Capture(asset, root.designWidth, root.designHeight, @out);
            return Path.GetFullPath(@out);
        }
    }
}
