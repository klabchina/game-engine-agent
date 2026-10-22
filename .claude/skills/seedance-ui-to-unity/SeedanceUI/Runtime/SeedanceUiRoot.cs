using UnityEngine;

namespace Seedance.UiImport
{
    /// <summary>Seedance UI Prefab 的根节点标记，记录来源 layout.json 的信息</summary>
    [DisallowMultipleComponent]
    public class SeedanceUiRoot : MonoBehaviour
    {
        public int designWidth;
        public int designHeight;
        public int layoutVersion;
    }
}
