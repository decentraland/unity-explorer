using DCL.Landscape.Settings;
using DCL.LOD;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using System;
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
        private const int LOD1_THRESHOLD_FOR_LOW_PRESET = 1;
        private const int LOD1_THRESHOLD_FOR_MEDIUM_PRESET = 1;
        private const int LOD1_THRESHOLD_FOR_HIGH_PRESET = 2;
        private const int LOD2_THRESHOLD_FOR_LOW_PRESET = 1;
        private const int LOD2_THRESHOLD_FOR_MEDIUM_PRESET = 3;
        private const int LOD2_THRESHOLD_FOR_HIGH_PRESET = 4;
        private const float UNITY_DEFAULT_LOD_BIAS = 0.8f;
        private const int TERRAIN_LOD_BIAS_FOR_LOW_PRESET = 50;
        private const int TERRAIN_LOD_BIAS_FOR_MEDIUM_PRESET = 150;
        private const int TERRAIN_LOD_BIAS_FOR_HIGH_PRESET = 250;
        private const int DETAIL_DENSITY_FOR_LOW_PRESET = 30;
        private const int DETAIL_DENSITY_FOR_MEDIUM_PRESET = 70;
        private const int DETAIL_DENSITY_FOR_HIGH_PRESET = 100;
        private const int GRASS_DISTANCE_FOR_LOW_PRESET = 75;
        private const int GRASS_DISTANCE_FOR_MEDIUM_PRESET = 150;
        private const int GRASS_DISTANCE_FOR_HIGH_PRESET = 300;
        private const int CHUNK_CULL_DISTANCE_FOR_LOW_PRESET = 1000;
        private const int CHUNK_CULL_DISTANCE_FOR_MEDIUM_PRESET = 3000;
        private const int CHUNK_CULL_DISTANCE_FOR_HIGH_PRESET = 7000;


        private readonly SettingsDropdownModuleView view;
        private readonly ISettingsDataStore settingsDataStore;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly LandscapeData landscapeData;

        public GraphicsQualitySettingsController(
            SettingsDropdownModuleView view,
            ISettingsDataStore settingsDataStore,
            int defaultQualityLevel,
            RealmPartitionSettingsAsset realmPartitionSettings,
            ILODSettingsAsset lodSettingsAsset,
            LandscapeData landscapeData)
        {
            this.view = view;
            this.settingsDataStore = settingsDataStore;
            this.realmPartitionSettings = realmPartitionSettings;
            this.lodSettingsAsset = lodSettingsAsset;
            this.landscapeData = landscapeData;

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
            //QualitySettings.SetQualityLevel(index);

            switch (index)
            {
                case 0: // Low
                    SetSceneLoadRadius(SCENE_LOAD_RADIUS_FOR_LOW_PRESET);
                    SetLodThreshold(LOD1_THRESHOLD_FOR_LOW_PRESET, 0);
                    SetLodThreshold(LOD2_THRESHOLD_FOR_LOW_PRESET, 1);
                    SetTerrainLodBias(TERRAIN_LOD_BIAS_FOR_LOW_PRESET);
                    SetDetailDensity(DETAIL_DENSITY_FOR_LOW_PRESET);
                    SetGrassDistance(GRASS_DISTANCE_FOR_LOW_PRESET);
                    SetChunkCullDistance(CHUNK_CULL_DISTANCE_FOR_LOW_PRESET);
                    break;
                case 1: // Medium
                    SetSceneLoadRadius(SCENE_LOAD_RADIUS_FOR_MEDIUM_PRESET);
                    SetLodThreshold(LOD1_THRESHOLD_FOR_MEDIUM_PRESET, 0);
                    SetLodThreshold(LOD2_THRESHOLD_FOR_MEDIUM_PRESET, 1);
                    SetTerrainLodBias(TERRAIN_LOD_BIAS_FOR_MEDIUM_PRESET);
                    SetDetailDensity(DETAIL_DENSITY_FOR_MEDIUM_PRESET);
                    SetGrassDistance(GRASS_DISTANCE_FOR_MEDIUM_PRESET);
                    SetChunkCullDistance(CHUNK_CULL_DISTANCE_FOR_MEDIUM_PRESET);
                    break;
                case 2: // High
                    SetSceneLoadRadius(SCENE_LOAD_RADIUS_FOR_HIGH_PRESET);
                    SetLodThreshold(LOD1_THRESHOLD_FOR_HIGH_PRESET, 0);
                    SetLodThreshold(LOD2_THRESHOLD_FOR_HIGH_PRESET, 1);
                    SetTerrainLodBias(TERRAIN_LOD_BIAS_FOR_HIGH_PRESET);
                    SetDetailDensity(DETAIL_DENSITY_FOR_HIGH_PRESET);
                    SetGrassDistance(GRASS_DISTANCE_FOR_HIGH_PRESET);
                    SetChunkCullDistance(CHUNK_CULL_DISTANCE_FOR_HIGH_PRESET);
                    break;
            }

            settingsDataStore.SetDropdownValue(GRAPHICS_QUALITY_DATA_STORE_KEY, index, save: true);
        }

        private void SetSceneLoadRadius(int maxLoadingDistanceInParcels)
        {
            realmPartitionSettings.MaxLoadingDistanceInParcels = maxLoadingDistanceInParcels;
        }

        private void SetLodThreshold(int lodThreshold, int index)
        {
            lodSettingsAsset.LodPartitionBucketThresholds[index] = lodThreshold;
        }

        private void SetTerrainLodBias(float lodBias)
        {
            float tempLodBias = UNITY_DEFAULT_LOD_BIAS * lodBias / 100f;
            if (!(Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f))
                return;

            QualitySettings.lodBias = tempLodBias;
        }

        private void SetDetailDensity(float detailDensity)
        {
            float tempDensity = detailDensity / 100f;
            if (!(Math.Abs(QualitySettings.terrainDetailDensityScale - tempDensity) > 0.005f))
                return;

            QualitySettings.terrainDetailDensityScale = tempDensity;
        }

        private void SetGrassDistance(float grassDistance)
        {
            if (!(Math.Abs(QualitySettings.terrainDetailDistance - grassDistance) > 0.005f))
                return;

            QualitySettings.terrainDetailDistance = grassDistance;
        }

        private void SetChunkCullDistance(int chunkCullDistance)
        {
            landscapeData.detailDistance = chunkCullDistance;
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveListener(SetGraphicsQualitySettings);
        }
    }
}
