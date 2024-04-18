using DCL.Settings.ModuleViews;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        private const int DEFAULT_QUALITY_LEVEL_INDEX = 1;
        private const string GRAPHICS_QUALITY_DATA_STORE_KEY = "Settings_GraphicsQuality";

        private readonly SettingsDropdownModuleView view;

        public GraphicsQualitySettingsController(SettingsDropdownModuleView view)
        {
            this.view = view;

            LoadGraphicsQualityOptions();

            view.DropdownView.Dropdown.value = settingsDataStore.HasKey(GRAPHICS_QUALITY_DATA_STORE_KEY) ?
                settingsDataStore.GetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY) :
                DEFAULT_QUALITY_LEVEL_INDEX;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetGraphicsQualitySettings);
            SetGraphicsQualitySettings(view.DropdownView.Dropdown.value);
        }

        private void LoadGraphicsQualityOptions()
        {
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in QualitySettings.names)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
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
