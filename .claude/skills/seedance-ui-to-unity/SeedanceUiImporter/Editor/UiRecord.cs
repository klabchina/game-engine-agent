// 一个导入 Prefab 的结构记录（Assets/SeedanceUI/<Prefab 路径>.json）。重导靠它把节点和 layout.json 的元素对上，
// 替代以前挂在节点上的标记组件——Prefab 上不挂任何 Seedance 脚本，工程里也不留 Seedance 代码。

using System;
using System.Collections.Generic;

namespace Seedance.UiImport.Editor
{
    [Serializable]
    public class UiRecordElement
    {
        /// <summary>layout.json 里的元素 id（e1、e2…），任务内稳定</summary>
        public string elementId;
        /// <summary>节点 GameObject 在 Prefab 里的 local fileID；程序改名、挪层级都不变</summary>
        public long fileId;
        /// <summary>ui-maker 里的元素名（中文），只供对照</summary>
        public string sourceName;
        /// <summary>首次导入时的元素类型（panel、button、text…）</summary>
        public string type;
        /// <summary>上次导入时 layout.json 里的父元素 id；挂在根下为 UiRecordStore.RootParentId</summary>
        public string layoutParentId;
    }

    [Serializable]
    public class UiRecord
    {
        public int formatVersion = 1;
        public string prefabGuid;
        public int designWidth, designHeight, layoutVersion;
        public List<UiRecordElement> elements = new List<UiRecordElement>();
    }
}
