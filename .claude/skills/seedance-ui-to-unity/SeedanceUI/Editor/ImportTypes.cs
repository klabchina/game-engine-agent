using System;

namespace Seedance.UiImport.Editor
{
    /// <summary>导入的输入有问题（产物格式、参数）。消息直接给用户看</summary>
    public class UiImportException : Exception
    {
        public UiImportException(string message) : base(message) { }
    }
}
