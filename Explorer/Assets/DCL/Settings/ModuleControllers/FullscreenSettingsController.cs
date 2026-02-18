using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using DCL.Utilities.Extensions;
using Global.AppArgs;
using Plugins.NativeWindowManager;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class FullscreenSettingsController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private readonly IAppArgs appParameters;

        private SettingsFeatureController resolutionController;

        private const float MIN_ASPECT_RATIO = 0.5f;
        private const float MAX_ASPECT_RATIO = 2f;
        private const int MIN_WIDTH = 500;
        private const int MIN_HEIGHT = 500;

        public FullscreenSettingsController(SettingsToggleModuleView view, IAppArgs appParameters)
        {
            this.view = view;
            this.appParameters = appParameters;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_FULLSCREEN))
                view.ToggleView.Toggle.isOn = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_FULLSCREEN);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetWindowModeSettingsOnValueChanged);
            SetFullscreen(view.ToggleView.Toggle.isOn, true);
        }

        private void SetWindowModeSettingsOnValueChanged(bool isOn)
        {
            SetFullscreen(isOn, false);
        }

        private void SetFullscreen(bool isOn, bool initialSetup)
        {
            if (appParameters.HasFlag(AppArgsFlags.WINDOWED_MODE) && initialSetup)
                return;

            Screen.fullScreenMode = isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_FULLSCREEN, isOn);

            if (!appParameters.HasFlag(AppArgsFlags.DISABLE_WINDOW_RESTRICTIONS))
                NativeWindowManager.ApplyConstraints(!isOn, MIN_ASPECT_RATIO, MAX_ASPECT_RATIO, MIN_WIDTH, MIN_HEIGHT);
        }

        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers)
        {
            foreach (var controller in controllers)
                if (controller is ResolutionSettingsController resolutionSettingsController)
                {
                    resolutionController = resolutionSettingsController;
                    break;
                }

            resolutionController.SetViewInteractable(!DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_VSYNC_ENABLED));
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveListener(SetWindowModeSettingsOnValueChanged);
        }
    }
}
