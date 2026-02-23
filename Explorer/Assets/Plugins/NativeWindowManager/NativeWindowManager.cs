using DCL.Prefs;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Plugins.NativeWindowManager
{
    /// <summary>
    /// Handles switching between fullscreen and windowed mode,
    /// and changing resolutions.
    /// </summary>
    public static class NativeWindowManager
    {
        private const float WINDOWED_RESOLUTION_RESIZE_COEFFICIENT = .75f;
        private const int MAX_WINDOWED_WIDTH = 2560;

        private const float MIN_ASPECT_RATIO = 4f / 3f;
        private const float MAX_ASPECT_RATIO = 21f / 9f;
        private const int MIN_WIDTH = 640;
        private const int MIN_HEIGHT = 480;

        /// <summary>
        /// Fires when the current resolution (Screen.width/height) changes.
        /// </summary>
        public static event Action<Vector2Int> CurrentResolutionChanged
        {
            add => resolutionListener.ResolutionChanged += value;
            remove => resolutionListener.ResolutionChanged -= value;
        }

        /// <summary>
        /// Fires when the fullscreen state changes.
        /// </summary>
        public static event Action<bool> FullScreenChanged;

        /// <summary>
        /// Gets or sets the fullscreen resolution.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if called in window mode.</exception>
        public static Vector2Int FullScreenResolution
        {
            get => DCLPlayerPrefs.GetVector2Int(DCLPrefKeys.SETTINGS_RESOLUTION, ResolutionUtils.GetDefaultResolution());

            set
            {
                if (Screen.fullScreenMode == FullScreenMode.Windowed)
                    throw new NotSupportedException("Trying to set fullscreen resolution while in windowed mode.");

                DCLPlayerPrefs.SetVector2Int(DCLPrefKeys.SETTINGS_RESOLUTION, value);
                Screen.SetResolution(value.x, value.y, FullScreenMode.FullScreenWindow);
            }
        }

        /// <summary>
        /// Gets the current resolution (Screen.width/height) as a Vector2Int.
        /// </summary>
        public static Vector2Int CurrentResolution => new (Screen.width, Screen.height);

        /// <summary>
        /// Enables or disables fullscreen mode (borderless).
        /// </summary>
        public static bool FullScreenEnabled
        {
            get => Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
            set => EnableFullscreen(value);
        }

        /// <summary>
        /// Returns a list of all available resolutions that can be used in
        /// fullscreen mode.
        /// </summary>
        public static List<Vector2Int> AvailableResolutions => ResolutionUtils.GetAvailableResolutions();

        private static bool initialized;
        private static bool wasFullScreenBeforeRequest;
        private static bool disableWindowConstraints;

        private static ResolutionListener resolutionListener;

        /// <summary>
        /// Initializes the window manager.
        /// </summary>
        /// <param name="disableConstraints">If constraints should be disabled in window mode.</param>
        /// <param name="windowedModeRequested">If window mode was specifically requested via app args.</param>
        /// <param name="isLocalScene">If we're running a local scene.</param>
        public static void Initialize(bool disableConstraints, bool windowedModeRequested, bool isLocalScene)
        {
            disableWindowConstraints = disableConstraints;

            if (windowedModeRequested || isLocalScene)
                EnableFullscreen(false, false);
            else if (!FullScreenEnabled)
                ApplyConstraints(true);

            var resolutionListenerGO = new GameObject("ResolutionListener");
            resolutionListener = resolutionListenerGO.AddComponent<ResolutionListener>();

            if (isLocalScene)
                FullScreenResolution = ResolutionUtils.GetDefaultResolution();
        }

        /// <summary>
        /// Requests a temporary window mode.
        /// </summary>
        public static void RequestTemporaryWindowMode()
        {
            if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow)
            {
                wasFullScreenBeforeRequest = false;
                return;
            }

            wasFullScreenBeforeRequest = true;

            Resolution current = Screen.currentResolution;

            var targetWidth = Mathf.Min((int)(current.width * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT), MAX_WINDOWED_WIDTH);
            var targetHeight = (int)(current.height * WINDOWED_RESOLUTION_RESIZE_COEFFICIENT);

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
        }

        /// <summary>
        /// Reverts a temporary window mode request, unless we were previously
        /// already in window mode.
        /// </summary>
        public static void TryRevertTemporaryWindowMode()
        {
            if (!wasFullScreenBeforeRequest || Screen.fullScreenMode == FullScreenMode.FullScreenWindow) return;

            Screen.SetResolution(FullScreenResolution.x, FullScreenResolution.y, FullScreenMode.FullScreenWindow);
        }

        private static void EnableFullscreen(bool enabled, bool store = true)
        {
            if (FullScreenEnabled == enabled) return;

            if (enabled)
            {
                Screen.SetResolution(FullScreenResolution.x, FullScreenResolution.y, FullScreenMode.FullScreenWindow);
                ApplyConstraints(false);
            }
            else
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                ApplyConstraints(true);
            }

            FullScreenChanged?.Invoke(enabled);

            if (store)
                DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_FULLSCREEN, enabled);
        }

        private static void ApplyConstraints(bool enabled)
        {
            if (disableWindowConstraints || Application.isEditor) return;

            if (!initialized)
            {
                WindowConstraint_Init();
                initialized = true;
            }

            WindowConstraint_Set(enabled ? 1 : 0, MIN_ASPECT_RATIO, MAX_ASPECT_RATIO, MIN_WIDTH, MIN_HEIGHT);
        }

        [DllImport("WindowResizeConstraint")]
        private static extern void WindowConstraint_Init();

        [DllImport("WindowResizeConstraint")]
        private static extern void WindowConstraint_Set(int enabled, float minAspect, float maxAspect, int minWidth, int minHeight);
    }
}
