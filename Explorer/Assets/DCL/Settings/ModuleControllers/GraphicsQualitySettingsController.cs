﻿using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Prefs;
using DCL.Quality;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using TMPro;
using UnityEngine;
using DCL.SkyBox;

namespace DCL.Settings.ModuleControllers
{
    public class GraphicsQualitySettingsController : SettingsFeatureController
    {
        // Used when system doesn't meet minimum requirements
        public const int MIN_SPECS_GRAPHICS_QUALITY_LEVEL = 0;

        private const int DEFAULT_QUALITY_LEVEL_INDEX = 1;

        private readonly SettingsDropdownModuleView view;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;
        private readonly LandscapeData landscapeData;
        private readonly QualitySettingsAsset qualitySettingsAsset;
        private readonly SkyboxSettingsAsset skyboxSettingsAsset;

        public GraphicsQualitySettingsController(SettingsDropdownModuleView view, RealmPartitionSettingsAsset realmPartitionSettingsAsset, LandscapeData landscapeData, QualitySettingsAsset qualitySettingsAsset, SkyboxSettingsAsset skyboxSettingsAsset)
        {
            this.view = view;

            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            this.landscapeData = landscapeData;
            this.qualitySettingsAsset = qualitySettingsAsset;
            this.skyboxSettingsAsset = skyboxSettingsAsset;

            LoadGraphicsQualityOptions();

            view.DropdownView.Dropdown.value = DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_GRAPHICS_QUALITY)
                ? DCLPlayerPrefs.GetInt(DCLPrefKeys.SETTINGS_GRAPHICS_QUALITY)
                :
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

            DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_GRAPHICS_QUALITY, index, save: true);

            // Update skybox refresh interval based on our quality preset
            // Mapping: Low(0)=5s, Medium(1 or others)=1s, High(2)=0s
            skyboxSettingsAsset.RefreshInterval = index switch
                                                  {
                                                      0 => 5f,
                                                      2 => 0f,
                                                      _ => 1f,
                                                  };
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
