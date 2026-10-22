// ui-maker 的 layout.json（version 1）。格式以上游 art-seedance-frameanimation 的 src/server/uiMakerExport.ts 为准：
// 坐标是设计分辨率下的画布绝对像素，原点左上、y 向下；elements 的数组顺序就是叠放顺序。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    [Serializable]
    public class UiRect
    {
        public int x, y, w, h;
    }

    [Serializable]
    public class UiResolution
    {
        public int width, height;
    }

    [Serializable]
    public class UiText
    {
        public string content;
        public float fontSize;
        public string color;
        /// <summary>left / center / right</summary>
        public string align;
        public bool bold;
    }

    [Serializable]
    public class UiLayoutElement
    {
        public string id, name, type, image, parent, sameAs;
        /// <summary>切图实际位置（含几像素留边）；missing 元素为 null</summary>
        public UiRect rect;
        /// <summary>用户画的框；text 元素等于 rect</summary>
        public UiRect drawnRect;
        public int order;
        public bool missing;
        /// <summary>只有 text 元素有</summary>
        public UiText text;

        public bool IsText => type == "text";
        public UiRect EffectiveRect => missing ? drawnRect : rect;
    }

    [Serializable]
    public class UiLayoutDoc
    {
        public int version;
        public UiResolution resolution;
        public List<UiLayoutElement> elements;

        public UiLayoutElement Find(string id) => id == null ? null : elements.Find(e => e.id == id);
    }

    public static class UiLayoutParser
    {
        public const int SupportedVersion = 1;

        public static UiLayoutDoc Parse(string json)
        {
            UiLayoutDoc doc;
            try { doc = JsonUtility.FromJson<UiLayoutDoc>(json); }
            catch (ArgumentException ex) { throw new UiImportException($"layout.json 不是合法的 JSON：{ex.Message}"); }
            if (doc == null) throw new UiImportException("layout.json 是空的");
            if (doc.version != SupportedVersion)
                throw new UiImportException($"layout.json 的 version 是 {doc.version}，只支持 {SupportedVersion}");
            // JsonUtility 不会把缺失的对象字段留成 null，而是填默认实例，所以按数值判断
            if (doc.resolution == null || doc.resolution.width <= 0 || doc.resolution.height <= 0)
                throw new UiImportException("layout.json 缺少 resolution（设计分辨率）");
            doc.elements ??= new List<UiLayoutElement>();

            var ids = new HashSet<string>();
            foreach (var e in doc.elements)
            {
                if (string.IsNullOrEmpty(e.id)) throw new UiImportException("layout.json 里有元素没有 id");
                if (!ids.Add(e.id)) throw new UiImportException($"layout.json 里元素 id {e.id} 重复");
                // JsonUtility 把 null 字符串读成 ""、null 对象读成默认实例，这里统一归一化回 null
                if (string.IsNullOrEmpty(e.parent)) e.parent = null;
                if (string.IsNullOrEmpty(e.sameAs)) e.sameAs = null;
                if (string.IsNullOrEmpty(e.image)) e.image = null;
                if (!e.IsText) e.text = null;
                if (e.missing) e.rect = null;
                if (e.IsText) e.drawnRect = e.rect;
            }
            foreach (var e in doc.elements)
            {
                if (e.parent != null && !ids.Contains(e.parent))
                    throw new UiImportException($"元素 {e.id} 的 parent {e.parent} 不存在");
                if (e.sameAs != null && !ids.Contains(e.sameAs))
                    throw new UiImportException($"元素 {e.id} 的 sameAs {e.sameAs} 不存在");
                if (e.EffectiveRect == null)
                    throw new UiImportException($"元素 {e.id} 没有位置（rect / drawnRect）");
            }
            return doc;
        }
    }
}
