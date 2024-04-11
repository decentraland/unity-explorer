using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        private const string GRAPHICS_QUALITY_DATA_STORE_KEY = "Settings_GraphicsQuality";
        private const int SCENE_LOAD_RADIUS_FOR_LOW_PRESET = 150;
        private const int SCENE_LOAD_RADIUS_FOR_MEDIUM_PRESET = 50;
        private const int SCENE_LOAD_RADIUS_FOR_HIGH_PRESET = 20;

        private readonly SettingsDropdownModuleView view;
        private readonly ISettingsDataStore settingsDataStore;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;

        public GraphicsQualitySettingsController(
            SettingsDropdownModuleView view,
            ISettingsDataStore settingsDataStore,
            int defaultQualityLevel,
            RealmPartitionSettingsAsset realmPartitionSettings)
        {
            this.view = view;
            this.settingsDataStore = settingsDataStore;
            this.realmPartitionSettings = realmPartitionSettings;

            // Clean current options loaded from the settings menu configuration and load names from QualitySettings
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in QualitySettings.names)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });

            view.DropdownView.Dropdown.value = settingsDataStore.GetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, defaultQualityLevel);
            view.DropdownView.Dropdown.onValueChanged.AddListener(SetQualityLevel);
            SetQualityLevel(view.DropdownView.Dropdown.value);
        }

        private void SetQualityLevel(int index)
        {
            QualitySettings.SetQualityLevel(index);

            switch (index)
            {
                case 0: // Low
                    realmPartitionSettings.MaxLoadingDistanceInParcels = SCENE_LOAD_RADIUS_FOR_LOW_PRESET;
                    break;
                case 1: // Medium
                    realmPartitionSettings.MaxLoadingDistanceInParcels = SCENE_LOAD_RADIUS_FOR_MEDIUM_PRESET;
                    break;
                case 2: // High
                    realmPartitionSettings.MaxLoadingDistanceInParcels = SCENE_LOAD_RADIUS_FOR_HIGH_PRESET;
                    break;
            }

            settingsDataStore.SetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, index, save: true);
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetQualityLevel);
        }
    }
}
