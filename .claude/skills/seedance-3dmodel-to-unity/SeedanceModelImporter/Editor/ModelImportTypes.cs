using System;
using System.Collections.Generic;

namespace Seedance.ModelImport.Editor
{
    /// <summary>导入的输入有问题（产物格式、参数、工程环境）。消息直接给用户看</summary>
    public class ModelImportException : Exception
    {
        public ModelImportException(string message) : base(message) { }
    }

    /// <summary>unity command seedance model_import 的请求（skill 写成 JSON 文件传进来）</summary>
    [Serializable]
    public class ModelImportRequest
    {
        /// <summary>FBX 产物的 zip 或解开后的目录，绝对路径</summary>
        public string source;
        /// <summary>工程内导入目录，必须以 Assets/ 开头</summary>
        public string importDir;
        /// <summary>可选，ASCII；为空取 importDir 最后一级</summary>
        public string prefabName;
    }

    /// <summary>一次导入的结果</summary>
    [Serializable]
    public class ModelImportReport
    {
        public string prefabPath;
        public string controllerPath;
        public string scenePath;
        /// <summary>模型包围盒高度（米）</summary>
        public float heightMeters;
        /// <summary>本次 FBX 里的动画，如 "idle (loop)"、"sit"</summary>
        public List<string> clips = new List<string>();
        /// <summary>Controller 里新建的状态</summary>
        public List<string> createdStates = new List<string>();
        /// <summary>Controller 里换了动作的状态</summary>
        public List<string> updatedStates = new List<string>();
        /// <summary>上次导入有、这次 FBX 里没有的动画（状态保留，动作置空）</summary>
        public List<string> missingClips = new List<string>();
        /// <summary>上次导入有、这次没有的骨骼名</summary>
        public List<string> removedBones = new List<string>();
        /// <summary>本次没用到的贴图（没删）</summary>
        public List<string> orphanTextures = new List<string>();
        public List<string> warnings = new List<string>();
    }
}
