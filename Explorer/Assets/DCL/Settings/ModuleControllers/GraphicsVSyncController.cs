using DCL.Settings.ModuleViews;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsVSyncController : SettingsFeatureController
    {
        private const string VSYNC_ENABLED_DATA_STORE_KEY = "Settings_VSync";

        private readonly SettingsToggleModuleView view;
        private SettingsFeatureController fpsLimitController;

        public GraphicsVSyncController(SettingsToggleModuleView view)
        {
            this.view = view;

            if (settingsDataStore.HasKey(VSYNC_ENABLED_DATA_STORE_KEY))
                view.ToggleView.Toggle.isOn = settingsDataStore.GetToggleValue(VSYNC_ENABLED_DATA_STORE_KEY);

            view.ToggleView.Toggle.onValueChanged.AddListener(SetVSyncEnabled);
            SetVSyncEnabled(view.ToggleView.Toggle.isOn);
        }

        private void SetVSyncEnabled(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            fpsLimitController?.SetViewInteractable(!enabled);
            settingsDataStore.SetToggleValue(VSYNC_ENABLED_DATA_STORE_KEY, enabled, save: true);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
        }

        public override void OnAllControllersInstantiated(List<SettingsFeatureController> controllers)
        {
            foreach (var controller in controllers)
                if (controller is FpsLimitSettingsController fpsController)
                {
                    fpsLimitController = fpsController;
                    break;
                }
            fpsLimitController?.SetViewInteractable(!settingsDataStore.GetToggleValue(VSYNC_ENABLED_DATA_STORE_KEY));
        }
    }
}
