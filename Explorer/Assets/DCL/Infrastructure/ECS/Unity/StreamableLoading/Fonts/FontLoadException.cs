using ECS.StreamableLoading.Common;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts
{
    public class FontLoadException : StreamableLoadingException
    {
        public FontLoadException(string message, LogType severity = LogType.Warning) : base(severity, message) { }

        public FontLoadException(string message, LogType severity, Exception innerException) : base(severity, message, innerException) { }
    }
}
