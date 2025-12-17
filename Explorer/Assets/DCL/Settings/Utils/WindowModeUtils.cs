using DCL.Prefs;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class WindowModeUtils
    {
        private const float WINDOWED_RESOLUTION_RESIZE_COEFFICIENT = .75f;
        private const int MAX_WINDOWED_WIDTH = 2560;
        private const FullScreenMode DEFAULT_SCREEN_MODE = FullScreenMode.FullScreenWindow;

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

        public static Resolution GetTargetResolution(List<Resolution> possibleResolutions)
        {
            return DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_RESOLUTION)
                ? GetSavedResolution()
                : GetDefaultResolution(possibleResolutions);

            Resolution GetSavedResolution()
            {
                int index = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_RESOLUTION);
                return index < 0 || index >= possibleResolutions.Count ? GetDefaultResolution(possibleResolutions) : possibleResolutions[index];
            }
        }

        public static Resolution GetDefaultResolution(List<Resolution> possibleResolutions)
        {
            int defaultIndex = 0;

            for (var index = 0; index < possibleResolutions.Count; index++)
            {
                Resolution resolution = possibleResolutions[index];

                if (!ResolutionUtils.IsDefaultResolution(resolution))
                    continue;

                defaultIndex = index;
                break;
            }

            return possibleResolutions[defaultIndex];
        }

        public static FullScreenMode GetTargetScreenMode(bool isAppArgWindowedMode)
        {
            // Check if windowed mode was requested via command line argument
            // If so, force windowed mode regardless of saved preferences
            if (isAppArgWindowedMode)
                return FullScreenMode.Windowed;

            return DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_WINDOW_MODE) ? GetSavedScreenMode() : DEFAULT_SCREEN_MODE;

            FullScreenMode GetSavedScreenMode()
            {
                int index = DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_WINDOW_MODE);
                return FullscreenModeUtils.Modes[index];
            }
        }
    }
}

