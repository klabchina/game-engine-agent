using System;
using System.Collections.Generic;
using UnityEngine;

namespace Seedance.UiImport.Editor
{
    /// <summary>导入的输入有问题（产物格式、参数）。消息直接给用户看</summary>
    public class UiImportException : Exception
    {
        public UiImportException(string message) : base(message) { }
    }

    /// <summary>unity command seedance ui_import 的请求（skill 写成 JSON 文件传进来）</summary>
    [Serializable]
    public class ImportRequest
    {
        /// <summary>ui-maker 任务的 out/ 目录或 ui.zip，绝对路径</summary>
        public string source;
        /// <summary>工程内导入目录，必须以 Assets/ 开头</summary>
        public string importDir;
        /// <summary>可选，ASCII；为空取 importDir 最后一级</summary>
        public string prefabName;
        /// <summary>可选，工程里现成的 TMP Font Asset 路径</summary>
        public string tmpFont;
        /// <summary>可选，没给 tmpFont 时用来生成动态 TMP 字体的字体文件（绝对路径）</summary>
        public string fontFile;
    }

    public class SpriteWriteResult
    {
        /// <summary>元素 id → Sprite。text 元素、missing 元素不在里面</summary>
        public Dictionary<string, Sprite> byElementId = new Dictionary<string, Sprite>();
        /// <summary>Sprites/ 下本次没用到的 png 资源路径（不删，交给人决定）</summary>
        public List<string> orphanSprites = new List<string>();
        public List<string> warnings = new List<string>();
    }

    /// <summary>一次导入的结果。列表项是 "&lt;id&gt; &lt;ui-maker 里的名字&gt;"，如 "e3 角色属性"</summary>
    [Serializable]
    public class ImportReport
    {
        public string prefabPath;
        public string scenePath;
        /// <summary>本次新建的节点</summary>
        public List<string> created = new List<string>();
        /// <summary>重导时更新了 Sprite / 位置 / 尺寸的节点</summary>
        public List<string> updated = new List<string>();
        /// <summary>产物里已经没有、被设为 inactive 的节点</summary>
        public List<string> deactivated = new List<string>();
        /// <summary>上游没出图的元素（只建了占位节点）</summary>
        public List<string> missing = new List<string>();
        /// <summary>类型和首次导入时不同的元素（组件没改）</summary>
        public List<string> typeChanged = new List<string>();
        /// <summary>被程序挪到别的父节点下、没更新位置的节点</summary>
        public List<string> moved = new List<string>();
        /// <summary>Sprites/ 下本次没用到的图（没删）</summary>
        public List<string> orphanSprites = new List<string>();
        public List<string> warnings = new List<string>();

        public static string Label(UiLayoutElement e) => $"{e.id} {e.name}";
    }
}
