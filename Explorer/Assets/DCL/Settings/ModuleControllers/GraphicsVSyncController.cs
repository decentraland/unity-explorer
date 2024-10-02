using DCL.Settings.ModuleViews;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsVSyncController : SettingsFeatureController
    {
        private const string VSYNC_ENABLED_DATA_STORE_KEY = "Settings_VSync";

        private readonly SettingsToggleModuleView view;

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
            settingsDataStore.SetToggleValue(VSYNC_ENABLED_DATA_STORE_KEY, enabled, save: true);
        }

        public override void Dispose()
        {
            view.ToggleView.Toggle.onValueChanged.RemoveAllListeners();
        }
    }
}
