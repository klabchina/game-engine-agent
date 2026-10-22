// Unity CLI 入口（需要 com.unity.pipeline）。导入逻辑在 Seedance.UiImport.Editor 里，这里只做参数和返回值。
//   unity command seedance_ui_prepare                 → "ready" 或 "importing"
//   unity command seedance_ui_import --request <json> → ImportReport 的 JSON

using System.IO;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    static class SeedanceUiImportCommand
    {
        [CliCommand("seedance_ui_prepare",
            "Make sure TMP Essential Resources are imported. Returns 'ready', or 'importing' (wait for the Editor to finish importing, then call again)")]
        static string Prepare()
        {
            if (FontResolver.TmpEssentialsReady()) return "ready";
            FontResolver.StartTmpEssentialsImport();
            return "importing";
        }

        [CliCommand("seedance_ui_import", "Import a Seedance ui-maker output (out/ dir or ui.zip) as a UGUI prefab and show it in Assets/Product/Scene/UIShowcase.unity")]
        static string Import([CliArg("request", "Absolute path to the import request JSON")] string request)
        {
            if (string.IsNullOrEmpty(request) || !File.Exists(request))
                throw new UiImportException($"请求文件不存在：{request}");
            var req = JsonUtility.FromJson<ImportRequest>(File.ReadAllText(request));
            return JsonUtility.ToJson(SeedanceUiImporter.Run(req), true);
        }
    }
}
