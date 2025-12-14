using System.Collections.Generic;
using DCL.Settings.Utils;
using DCL.Utility;
using Global.AppArgs;
using UnityEngine;

namespace DCL.Settings.ScreenMode
{
    public class ScreenModeController : IScreenModeController
    {
        private readonly IAppArgs appArgs;
        private readonly List<Resolution> possibleResolutions = new ();

        public ScreenModeController(IAppArgs appArgs)
        {
            this.appArgs = appArgs;
            possibleResolutions.AddRange(ResolutionUtils.GetAvailableResolutions());
        }

        public void ApplyWindowedMode()
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
                WindowModeUtils.ApplyWindowedMode();
        }

        public void RestoreResolutionAndScreenMode()
        {
            var targetResolution = WindowModeUtils.GetTargetResolution(possibleResolutions);
            var targetScreenMode = WindowModeUtils.GetTargetScreenMode(appArgs.HasFlag(AppArgsFlags.WINDOWED_MODE));
            Screen.SetResolution(targetResolution.width, targetResolution.height, targetScreenMode, targetResolution.refreshRateRatio);
        }
    }
}