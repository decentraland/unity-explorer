using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Utilities
{
    public static class ScrollRectExtensions
    {
        private const int MACOS_SCROLL_SENSITIVITY = 3;
        private const int WINDOWS_SCROLL_SENSITIVITY = 1;

        public static void SetScrollSensitivityBasedOnPlatform(this ScrollRect scrollRect, float overrideWindows = WINDOWS_SCROLL_SENSITIVITY, float overrideMacOS = MACOS_SCROLL_SENSITIVITY)
        {
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                scrollRect.scrollSensitivity = overrideWindows;
            else
                scrollRect.scrollSensitivity = overrideMacOS;
        }
    }
}
