using DCL.Prefs;
using DCL.Settings.ModuleViews;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsVSyncController : SettingsFeatureController
    {
        private readonly SettingsToggleModuleView view;
        private SettingsFeatureController fpsLimitController;
        private int previousTargetFrameRate;

        public GraphicsVSyncController(SettingsToggleModuleView view)
        {
            this.view = view;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_VSYNC_ENABLED))
                view.ToggleView.Toggle.isOn = DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_VSYNC_ENABLED);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetVSyncEnabled);
        }

        private void SetVSyncEnabled(bool enabled)
        {
            //Target frame rate is also modified because despite what the documentation says, it changes how the VSync performs
            if (enabled)
            {
                QualitySettings.vSyncCount = 1;
                previousTargetFrameRate = Application.targetFrameRate;
                Application.targetFrameRate = 0;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = previousTargetFrameRate;
            }

            fpsLimitController?.SetViewInteractable(!enabled);
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_VSYNC_ENABLED, enabled, save: true);
        }

        public override void Dispose() =>
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();

        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers)
        {
            foreach (var controller in controllers)
                if (controller is FpsLimitSettingsController fpsController)
                {
                    fpsLimitController = fpsController;
                    break;
                }

            fpsLimitController?.SetViewInteractable(!DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_VSYNC_ENABLED));

            SettingsDropdownModuleView fpsLimitView = (SettingsDropdownModuleView) fpsLimitController?.controllerView;
            previousTargetFrameRate = 0;
            if (fpsLimitView?.DropdownView.Dropdown.value != 0)
                previousTargetFrameRate = Convert.ToInt32(fpsLimitView?.DropdownView.Dropdown.options[fpsLimitView.DropdownView.Dropdown.value].text);

            SetVSyncEnabled(view.ToggleView.Toggle.isOn);
        }
    }
}
