using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Utilities
{
    public static class ScrollRectExtensions
    {
        private const int MACOS_SCROLL_SENSITIVITY = 3;
        private const int WINDOWS_SCROLL_SENSITIVITY = 1;

        public static void SetScrollSensitivityBasedOnPlatform(this ScrollRect scrollRect)
        {
            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                scrollRect.scrollSensitivity = MACOS_SCROLL_SENSITIVITY;
            else
                scrollRect.scrollSensitivity = WINDOWS_SCROLL_SENSITIVITY;
        }
    }
}
