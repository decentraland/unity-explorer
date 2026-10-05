using ECS.StreamableLoading.Common;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts
{
    public class FontLoadException : StreamableLoadingException
    {
        public FontLoadException(string message, LogType severity = LogType.Warning) : base(severity, message) { }
    }
}
