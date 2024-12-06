using UnityEngine;

namespace DCL.Clipboard
{
    public class UnityClipboard : ISystemClipboard
    {
        public void Set(string text) =>
            GUIUtility.systemCopyBuffer = text;
    }
}
