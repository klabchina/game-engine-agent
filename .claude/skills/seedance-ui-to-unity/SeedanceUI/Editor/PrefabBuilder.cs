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
        /// 重导：按 elementId 找回节点，只更新 Sprite、位置、尺寸。文字、同级顺序、显隐、已有组件都归程序，不碰；
        /// 父节点只在程序没挪过时跟着上游改。没有 SeedanceUiElement 的节点（手加的）、嵌套进来的别的 Seedance Prefab 完全不碰。
        /// </summary>
        static GameObject Update(UiLayoutDoc doc, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            string prefabPath, ImportReport report)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var rootInfo = root.GetComponent<SeedanceUiRoot>();
                var nodes = new Dictionary<string, GameObject>();
                foreach (var m in root.GetComponentsInChildren<SeedanceUiElement>(true))
                {
                    // 程序可能把别的导入出来的 Prefab 嵌进来，它们的 id 也是 e1…eN，只认归属本根的标记
                    if (m.GetComponentInParent<SeedanceUiRoot>(true) != rootInfo) continue;
                    if (string.IsNullOrEmpty(m.elementId) || nodes.ContainsKey(m.elementId))
                    {
                        report.warnings.Add($"节点 {m.name} 的 elementId「{m.elementId}」为空或重复，跳过");
                        continue;
                    }
                    nodes[m.elementId] = m.gameObject;
                }
                var docIds = new HashSet<string>();
                foreach (var e in doc.elements) docIds.Add(e.id);
                Transform LayoutParent(UiLayoutElement e) => e.parent != null ? nodes[e.parent].transform : root.transform;

                var added = new HashSet<string>();
                foreach (var e in doc.elements)
                {
                    if (nodes.ContainsKey(e.id)) continue;
                    nodes[e.id] = CreateNode(e, sprites, font, report);
                    added.Add(e.id);
                }
                // 按数组顺序处理：新节点、跟着上游换了父级的节点，都插到同级里排在它前面的那个元素之后
                foreach (var e in doc.elements)
                {
                    var node = nodes[e.id];
                    if (added.Contains(e.id))
                    {
                        node.transform.SetParent(LayoutParent(e), false);
                        InsertInLayoutOrder(doc, e, node.transform, nodes);
                        Place(doc, e, (RectTransform)node.transform);
                        continue;
                    }
                    UpdateNode(doc, e, node, LayoutParent(e), root.transform, nodes, sprites, font, report);
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

        static void UpdateNode(UiLayoutDoc doc, UiLayoutElement e, GameObject node, Transform layoutParent, Transform root,
            Dictionary<string, GameObject> nodes, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font, ImportReport report)
        {
            var marker = node.GetComponent<SeedanceUiElement>();
            if (marker.type != e.type)
            {
                report.typeChanged.Add(ImportReport.Label(e));
                report.warnings.Add($"{ImportReport.Label(e)}：类型从 {marker.type} 变成了 {e.type}，组件没改，需要的话手动调整");
            }
            if (e.missing) report.missing.Add(ImportReport.Label(e));

            // 首次导入时是 missing 的占位节点（没有任何 Graphic），上游补出图后按类型补上组件
            if (!e.missing && !e.IsText && node.GetComponent<Graphic>() == null && sprites.ContainsKey(e.id))
                AddComponents(node, e, sprites, font, report);
            var img = node.GetComponent<Image>();
            if (img != null && sprites.TryGetValue(e.id, out var sprite)) img.sprite = sprite;

            var actual = node.transform.parent;
            if (actual != layoutParent)
            {
                var recorded = RecordedParent(marker, root, nodes);
                if (recorded == null || actual != recorded)
                {
                    // 程序挪走的：位置不动，尺寸照样更新（pivot 在左上，左上角不会跑）——上游每次重出切图留边都不同
                    ((RectTransform)node.transform).sizeDelta = UiGeometry.Place(e.EffectiveRect, null).size;
                    report.moved.Add(ImportReport.Label(e));
                    return;
                }
                // 还在上次的 layout 父节点下，说明是上游改了父级：跟过去
                node.transform.SetParent(layoutParent, false);
                InsertInLayoutOrder(doc, e, node.transform, nodes);
            }
            marker.layoutParentId = e.parent ?? RootParentId;
            Place(doc, e, (RectTransform)node.transform);
            report.updated.Add(ImportReport.Label(e));
        }

        const string RootParentId = "<root>";

        static Transform RecordedParent(SeedanceUiElement marker, Transform root, Dictionary<string, GameObject> nodes)
        {
            if (string.IsNullOrEmpty(marker.layoutParentId)) return null;
            if (marker.layoutParentId == RootParentId) return root;
            return nodes.TryGetValue(marker.layoutParentId, out var p) ? p.transform : null;
        }

        /// <summary>插到同级里、数组中排在 e 前面且同父的最近那个元素之后；没有就放到最前（叠放最底）</summary>
        static void InsertInLayoutOrder(UiLayoutDoc doc, UiLayoutElement e, Transform t, Dictionary<string, GameObject> nodes)
        {
            var index = doc.elements.IndexOf(e);
            for (var i = index - 1; i >= 0; i--)
            {
                var prev = doc.elements[i];
                if (prev.parent != e.parent || !nodes.TryGetValue(prev.id, out var prevNode) || prevNode.transform.parent != t.parent) continue;
                t.SetSiblingIndex(prevNode.transform.GetSiblingIndex() + 1);
                return;
            }
            t.SetAsFirstSibling();
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
            marker.layoutParentId = e.parent ?? RootParentId;
            report.created.Add(ImportReport.Label(e));

            if (e.missing) report.missing.Add(ImportReport.Label(e));
            else AddComponents(go, e, sprites, font, report);
            return go;
        }

        /// <summary>按类型挂组件。首次建节点时用；missing 占位节点在上游补出图后的重导里也用</summary>
        static void AddComponents(GameObject go, UiLayoutElement e, IReadOnlyDictionary<string, Sprite> sprites, TMP_FontAsset font,
            ImportReport report)
        {
            if (e.IsText)
            {
                AddText(go, e, font, report);
                return;
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
        }

        const float DefaultFontSize = 36f;

        static void AddText(GameObject go, UiLayoutElement e, TMP_FontAsset font, ImportReport report)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            var s = e.text;
            if (font != null) t.font = font;
            t.text = s?.content ?? "";
            if (s != null && s.fontSize > 0) t.fontSize = s.fontSize;
            else
            {
                // JsonUtility 把缺失的 text 读成默认实例，字号是 0，文字会看不见
                t.fontSize = DefaultFontSize;
                report.warnings.Add($"{ImportReport.Label(e)}：没有字号，用了 {DefaultFontSize}");
            }
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
