using DCL.Prefs;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Plugins.NativeWindowManager
{
    public static class NativeWindowManager
    {
        private const float WINDOWED_RESOLUTION_RESIZE_COEFFICIENT = .75f;
        private const int MAX_WINDOWED_WIDTH = 2560;

        private const float MIN_ASPECT_RATIO = 4f / 3f;
        private const float MAX_ASPECT_RATIO = 21f / 9f;
        private const int MIN_WIDTH = 640;
        private const int MIN_HEIGHT = 480;

        public static event Action<Vector2Int> CurrentResolutionChanged
        {
            add => resolutionListener.ResolutionChanged += value;
            remove => resolutionListener.ResolutionChanged -= value;
        }

        public static event Action<bool> FullScreenChanged;

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

        public static Vector2Int CurrentResolution => new (Screen.width, Screen.height);

        public static bool FullScreenEnabled
        {
            get => Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
            set => EnableFullscreen(value);
        }

        public static List<Vector2Int> AvailableResolutions => ResolutionUtils.GetAvailableResolutions();

        private static bool initialized;
        private static bool wasFullScreenBeforeRequest;
        private static bool disableWindowConstraints;

        private static ResolutionListener resolutionListener;

        public static void Initialize(bool disableConstraints, bool windowedModeRequested)
        {
            disableWindowConstraints = disableConstraints;

            if (windowedModeRequested)
                EnableFullscreen(false, false);
            else if (!FullScreenEnabled)
                ApplyConstraints(true);

            var resolutionListenerGO = new GameObject("ResolutionListener");
            resolutionListener = resolutionListenerGO.AddComponent<ResolutionListener>();

            //             //When running multiple instances from local scene, the last one opened will set the resolution to the lowest available
            // bool isLocalScene = appParameters.HasFlag(AppArgsFlags.LOCAL_SCENE);
            // int startIndex = isLocalScene ? possibleResolutions.Count - 1 : 0;
            // int endIndex = isLocalScene ? -1 : possibleResolutions.Count;
            // int step = isLocalScene ? -1 : 1;
            //
            // for (int index = startIndex; index != endIndex; index += step)
            // {
            //     Resolution resolution = possibleResolutions[index];
            //
            //     if (!ResolutionUtils.IsDefaultResolution(resolution))
            //         continue;
            //
            //     view.DropdownView.Dropdown.value = index;
            //     break;
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

        public static void TryRevertTemporaryWindowMode()
        {
            if (!wasFullScreenBeforeRequest || Screen.fullScreenMode == FullScreenMode.FullScreenWindow) return;

            Screen.SetResolution(FullScreenResolution.x, FullScreenResolution.y, FullScreenMode.FullScreenWindow);
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
