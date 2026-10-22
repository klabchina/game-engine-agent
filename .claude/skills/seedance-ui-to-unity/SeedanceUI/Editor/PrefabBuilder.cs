// 按 layout.json 搭 UGUI Prefab。根节点是普通 RectTransform（四边拉伸），不带 Canvas——
// 由放它的 Canvas（展示场景里参考分辨率 = 设计分辨率）负责缩放。

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Seedance.UiImport.Editor
{
    public static class PrefabBuilder
    {
        public static GameObject BuildOrUpdate(UiLayoutDoc doc, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            string prefabPath, ImportReport report)
        {
            report.prefabPath = prefabPath;
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
                ? Update(doc, sprites, font, prefabPath, report)
                : Create(doc, sprites, font, prefabPath, report);
        }

        /// <summary>
        /// 重导：按 elementId 找回节点，只更新 Sprite、位置、尺寸。文字、父节点、同级顺序、组件、显隐都归程序，不碰；
        /// 没有 SeedanceUiElement 的节点（手加的）完全不碰。
        /// </summary>
        static GameObject Update(UiLayoutDoc doc, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            string prefabPath, ImportReport report)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var nodes = new Dictionary<string, GameObject>();
                foreach (var m in root.GetComponentsInChildren<SeedanceUiElement>(true))
                {
                    if (string.IsNullOrEmpty(m.elementId) || nodes.ContainsKey(m.elementId))
                    {
                        report.warnings.Add($"节点 {m.name} 的 elementId「{m.elementId}」为空或重复，跳过");
                        continue;
                    }
                    nodes[m.elementId] = m.gameObject;
                }
                var docIds = new HashSet<string>();
                foreach (var e in doc.elements) docIds.Add(e.id);

                var added = new List<UiLayoutElement>();
                foreach (var e in doc.elements)
                {
                    if (nodes.ContainsKey(e.id)) continue;
                    nodes[e.id] = CreateNode(e, sprites, font, report);
                    added.Add(e);
                }
                foreach (var e in added)
                {
                    var parent = e.parent != null ? nodes[e.parent].transform : root.transform;
                    nodes[e.id].transform.SetParent(parent, false);
                    nodes[e.id].transform.SetAsLastSibling();
                    Place(doc, e, (RectTransform)nodes[e.id].transform);
                }

                foreach (var e in doc.elements)
                {
                    if (added.Contains(e)) continue;
                    UpdateNode(doc, e, nodes[e.id], e.parent != null ? nodes[e.parent].transform : root.transform, sprites, report);
                }

                foreach (var kv in nodes)
                {
                    if (docIds.Contains(kv.Key)) continue;
                    kv.Value.SetActive(false);
                    var m = kv.Value.GetComponent<SeedanceUiElement>();
                    report.deactivated.Add($"{m.elementId} {m.sourceName}");
                }
                return Save(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void UpdateNode(UiLayoutDoc doc, UiLayoutElement e, GameObject node, Transform layoutParent,
            IReadOnlyDictionary<string, Sprite> sprites, ImportReport report)
        {
            var marker = node.GetComponent<SeedanceUiElement>();
            if (marker.type != e.type)
            {
                report.typeChanged.Add(ImportReport.Label(e));
                report.warnings.Add($"{ImportReport.Label(e)}：类型从 {marker.type} 变成了 {e.type}，组件没改，需要的话手动调整");
            }
            if (e.missing) report.missing.Add(ImportReport.Label(e));

            var img = node.GetComponent<Image>();
            if (img != null && sprites.TryGetValue(e.id, out var sprite)) img.sprite = sprite;

            if (node.transform.parent != layoutParent)
            {
                report.moved.Add(ImportReport.Label(e));
                return;
            }
            Place(doc, e, (RectTransform)node.transform);
            report.updated.Add(ImportReport.Label(e));
        }

        static GameObject Create(UiLayoutDoc doc, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            string prefabPath, ImportReport report)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath), typeof(RectTransform));
            try
            {
                UiGeometry.Stretch((RectTransform)root.transform);
                var info = root.AddComponent<SeedanceUiRoot>();
                info.designWidth = doc.resolution.width;
                info.designHeight = doc.resolution.height;
                info.layoutVersion = doc.version;

                // 两遍：先全部建出来，再按数组顺序挂父节点，同级顺序因此等于数组（叠放）顺序
                var nodes = new Dictionary<string, GameObject>();
                foreach (var e in doc.elements) nodes[e.id] = CreateNode(e, sprites, font, report);
                foreach (var e in doc.elements)
                {
                    var parent = e.parent != null ? nodes[e.parent].transform : root.transform;
                    nodes[e.id].transform.SetParent(parent, false);
                    nodes[e.id].transform.SetAsLastSibling();
                    Place(doc, e, (RectTransform)nodes[e.id].transform);
                }
                return Save(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject Save(GameObject root, string prefabPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var ok);
            if (!ok) throw new UiImportException($"保存 Prefab 失败：{prefabPath}");
            return prefab;
        }

        static void Place(UiLayoutDoc doc, UiLayoutElement e, RectTransform rt) =>
            UiGeometry.Apply(rt, UiGeometry.Place(e.EffectiveRect, doc.Find(e.parent)?.EffectiveRect));

        /// <summary>首次建节点：名字、标记、按类型挂组件。之后这些都归程序，重导不再碰</summary>
        internal static GameObject CreateNode(UiLayoutElement e, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            ImportReport report)
        {
            var go = new GameObject(UiNaming.NodeName(e), typeof(RectTransform));
            var marker = go.AddComponent<SeedanceUiElement>();
            marker.elementId = e.id;
            marker.sourceName = e.name;
            marker.type = e.type;
            report.created.Add(ImportReport.Label(e));

            if (e.missing)
            {
                report.missing.Add(ImportReport.Label(e));
                return go;
            }
            if (e.IsText)
            {
                AddText(go, e, font, report);
                return go;
            }

            var img = go.AddComponent<Image>();
            if (sprites.TryGetValue(e.id, out var sprite)) img.sprite = sprite;
            else report.warnings.Add($"{ImportReport.Label(e)}：没有切图");
            img.raycastTarget = e.type == "button" || e.type == "tab";
            switch (e.type)
            {
                case "button":
                    go.AddComponent<Button>().targetGraphic = img;
                    break;
                case "tab":
                    var toggle = go.AddComponent<Toggle>();
                    toggle.targetGraphic = img;
                    toggle.graphic = null;
                    break;
                case "bar":
                    img.type = Image.Type.Filled;
                    img.fillMethod = Image.FillMethod.Horizontal;
                    img.fillOrigin = (int)Image.OriginHorizontal.Left;
                    img.fillAmount = 1f;
                    break;
            }
            return go;
        }

        static void AddText(GameObject go, UiLayoutElement e, TMP_FontAsset font, ImportReport report)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            var s = e.text;
            if (font != null) t.font = font;
            t.text = s?.content ?? "";
            t.fontSize = s?.fontSize ?? 36;
            if (s != null && ColorUtility.TryParseHtmlString(s.color, out var c)) t.color = c;
            else
            {
                t.color = Color.white;
                report.warnings.Add($"{ImportReport.Label(e)}：颜色 {s?.color} 解析不了，用了白色");
            }
            // TMP 的 Left / Center / Right 本身就是垂直居中，和 ui-maker 预览一致
            t.alignment = s?.align == "left" ? TextAlignmentOptions.Left
                : s?.align == "right" ? TextAlignmentOptions.Right
                : TextAlignmentOptions.Center;
            t.fontStyle = s != null && s.bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
        }
    }
}
