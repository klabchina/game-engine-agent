// 一个导入出来的模型 Prefab 的记录（重导时分辨哪些东西是导入生成的、哪些是程序加的）。
// 存在 Assets/SeedanceKlab/Model/<Prefab 路径去掉 Assets/>.json，纯 JSON，不依赖任何脚本。

using System;
using System.Collections.Generic;

namespace Seedance.ModelImport.Editor
{
    [Serializable]
    public class RoleGuid
    {
        /// <summary>TextureRole 的名字：BaseColor / Normal / Metallic</summary>
        public string role;
        public string guid;
    }

    [Serializable]
    public class ModelRecord
    {
        public int formatVersion = 1;
        public string prefabGuid;
        public string fbxGuid;
        /// <summary>导入建的 AnimatorController；重导只往这份里写</summary>
        public string controllerGuid;
        /// <summary>"tripo" / "hunyuan" / "unknown"，只作参考</summary>
        public string source;
        /// <summary>导入生成的动画 / 状态名</summary>
        public List<string> clips = new List<string>();
        /// <summary>上次导入按用途写进 Textures/ 的贴图</summary>
        public List<RoleGuid> textures = new List<RoleGuid>();
        /// <summary>导入建的材质 GUID</summary>
        public List<string> materials = new List<string>();
        /// <summary>上次导入的骨骼名（排序去重），用来发现骨骼改名</summary>
        public List<string> bones = new List<string>();
        /// <summary>导入时给 &lt;P&gt;_Model 子节点设的朝向（绕 Y 轴的度数），重导时用来判断程序有没有转过它</summary>
        public float facingYaw;
    }
}
