using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Quality;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        private const int DEFAULT_QUALITY_LEVEL_INDEX = 1;
        private const string GRAPHICS_QUALITY_DATA_STORE_KEY = "Settings_GraphicsQuality";

        private readonly SettingsDropdownModuleView view;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        private readonly LandscapeData landscapeData;
        private readonly QualitySettingsAsset qualitySettingsAsset;

        public GraphicsQualitySettingsController(SettingsDropdownModuleView view, RealmPartitionSettingsAsset realmPartitionSettingsAsset, LandscapeData landscapeData, QualitySettingsAsset qualitySettingsAsset)
        {
            this.view = view;

            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            this.landscapeData = landscapeData;
            this.qualitySettingsAsset = qualitySettingsAsset;

            LoadGraphicsQualityOptions();

            view.DropdownView.Dropdown.value = settingsDataStore.HasKey(GRAPHICS_QUALITY_DATA_STORE_KEY) ?
                settingsDataStore.GetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY) :
                DEFAULT_QUALITY_LEVEL_INDEX;

            view.DropdownView.Dropdown.onValueChanged.AddListener(SetGraphicsQualitySettings);
            SetGraphicsQualitySettings(view.DropdownView.Dropdown.value);

            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged += OnMaxLoadingDistanceInParcelsChanged;
            landscapeData.OnDetailDistanceChanged += OnDetailDistanceChanged;
        }

        private void LoadGraphicsQualityOptions()
        {
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in QualitySettings.names)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
            view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = "Custom" });
        }

        private void SetGraphicsQualitySettings(int index)
        {
            if (index < view.DropdownView.Dropdown.options.Count - 1)
                ForceSetQualityLevel(index);

            settingsDataStore.SetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, index, save: true);
        }

        private void ForceSetQualityLevel(int index)
        {
            int currentQualityLevel = QualitySettings.GetQualityLevel();
            if (currentQualityLevel == index)
            {
                if (currentQualityLevel < qualitySettingsAsset.customSettings.Count - 1)
                    QualitySettings.SetQualityLevel(index + 1);
                else
                    QualitySettings.SetQualityLevel(0);
            }

            QualitySettings.SetQualityLevel(index);
        }

        private void OnMaxLoadingDistanceInParcelsChanged(int _)
        {
            if (!view.gameObject.activeInHierarchy)
                return;

            CheckIfCustomPresetAsync().Forget();
        }

        private void OnDetailDistanceChanged(float _)
        {
            if (!view.gameObject.activeInHierarchy)
                return;

            CheckIfCustomPresetAsync().Forget();
        }

        private async UniTaskVoid CheckIfCustomPresetAsync()
        {
            // Wait for the next frame to ensure that the quality settings have been updated
            await UniTask.NextFrame();

            var lastQualityLevel = qualitySettingsAsset.customSettings[QualitySettings.GetQualityLevel()];
            if (lastQualityLevel.environmentSettings.sceneLoadRadius == realmPartitionSettingsAsset.MaxLoadingDistanceInParcels &&
                Mathf.Approximately(lastQualityLevel.environmentSettings.chunkCullDistance, landscapeData.DetailDistance))
                return;

            // Set the custom label
            view.DropdownView.Dropdown.value = view.DropdownView.Dropdown.options.Count - 1;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetGraphicsQualitySettings);
            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged -= OnMaxLoadingDistanceInParcelsChanged;
            landscapeData.OnDetailDistanceChanged -= OnDetailDistanceChanged;
        }
    }
}
