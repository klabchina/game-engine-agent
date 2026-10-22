using UnityEngine;

namespace Seedance.UiImport
{
    /// <summary>
    /// 由 Seedance UI 导入器建的节点都挂这个标记。重导时按 elementId 找回节点；
    /// 没有这个组件的节点（程序手加的）导入器完全不碰。
    /// </summary>
    [DisallowMultipleComponent]
    public class SeedanceUiElement : MonoBehaviour
    {
        /// <summary>layout.json 里的元素 id（e1、e2…），任务内稳定</summary>
        public string elementId;
        /// <summary>ui-maker 里的元素名（中文），只供对照</summary>
        public string sourceName;
        /// <summary>首次导入时的元素类型（panel、button、text…）</summary>
        public string type;
        /// <summary>
        /// 上次导入时 layout.json 里的父元素 id（挂在根下为 "&lt;root&gt;"）。重导时节点的实际父节点还是它，
        /// 说明程序没挪过，可以跟着上游改父级；对不上才算程序挪走的
        /// </summary>
        public string layoutParentId;
    }
}
