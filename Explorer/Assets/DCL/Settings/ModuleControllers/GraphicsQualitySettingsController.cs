using DCL.Settings.ModuleViews;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        private const string GRAPHICS_QUALITY_DATA_STORE_KEY = "Settings_GraphicsQuality";

        private readonly SettingsDropdownModuleView view;
        private readonly ISettingsDataStore settingsDataStore;

        public GraphicsQualitySettingsController(
            SettingsDropdownModuleView view,
            ISettingsDataStore settingsDataStore,
            int defaultQualityLevel)
        {
            this.view = view;
            this.settingsDataStore = settingsDataStore;

            // Clean current options loaded from the settings menu configuration and load names from QualitySettings
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in QualitySettings.names)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });

            view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, defaultQualityLevel);
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetGraphicsQualitySettings);
            SetGraphicsQualitySettings(view.DropdownView.Dropdown.value);
        }

        private void SetGraphicsQualitySettings(int index)
        {
            QualitySettings.SetQualityLevel(index);
            settingsDataStore.SetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetGraphicsQualitySettings);
        }
    }
}
