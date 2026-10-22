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

    public class SpriteWriteResult
    {
        /// <summary>元素 id → Sprite。text 元素、missing 元素不在里面</summary>
        public Dictionary<string, Sprite> byElementId = new Dictionary<string, Sprite>();
        /// <summary>Sprites/ 下本次没用到的 png 资源路径（不删，交给人决定）</summary>
        public List<string> orphanSprites = new List<string>();
        public List<string> warnings = new List<string>();
    }
}
