using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class WindowModeUtils
    {
        private const float WINDOWED_RESOLUTION_RESIZE_COEFFICIENT = .75f;
        private const int MAX_WINDOWED_WIDTH = 2560;

        /// <summary>
        /// Sets the game to windowed mode with appropriate resolution adjustments.
        /// To avoid ultra-wide windows on ultra-wide screens, the resolution is resized to 75% of the current resolution
        /// with a maximum width of 2560 pixels.
        /// </summary>
        public static void ApplyWindowedMode()
        {
            Resolution current = Screen.currentResolution;

            int targetWidth = Mathf.Min((int)(current.width * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT), MAX_WINDOWED_WIDTH);
            int targetHeight = (int)(current.height * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT);

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed, current.refreshRateRatio);
        }
    }
}

