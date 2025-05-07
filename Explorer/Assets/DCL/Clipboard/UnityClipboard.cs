using UnityEngine;

namespace DCL.Clipboard
{
    public class UnityClipboard : ISystemClipboard
    {
        public void Set(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        public string Get() =>
            GUIUtility.systemCopyBuffer;

        public bool HasValue() => GUIUtility.systemCopyBuffer?.Length > 0;
    }
}
