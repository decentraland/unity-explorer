using ECS.StreamableLoading.Common;
using UnityEngine;

namespace ECS.StreamableLoading.Fonts
{
    public class FontLoadException : StreamableLoadingException
    {
        public FontLoadException(string message) : base(LogType.Warning, message) { }
    }
}
