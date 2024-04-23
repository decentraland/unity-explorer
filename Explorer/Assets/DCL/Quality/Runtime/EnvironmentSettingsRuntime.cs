using DCL.DebugUtilities;
using DCL.Landscape.Settings;
using DCL.LOD;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public class EnvironmentSettingsRuntime : IQualitySettingRuntime
    {
        private const float UNITY_DEFAULT_LOD_BIAS = 0.8f;

        private PersistentSetting<int> sceneLoadRadius;
        private PersistentSetting<int> lod1Threshold;
        private PersistentSetting<int> lod2Threshold;
        private PersistentSetting<float> terrainLODBias;
        private PersistentSetting<float> detailDensity;
        private PersistentSetting<float> grassDistance;
        private PersistentSetting<float> chunkCullDistance;

        private readonly RealmPartitionSettingsAsset? realmPartitionSettings;
        private readonly ILODSettingsAsset? lodSettingsAsset;
        private readonly LandscapeData? landscapeData;

        public bool IsActive => true;

        public EnvironmentSettingsRuntime(
            RealmPartitionSettingsAsset? realmPartitionSettings,
            ILODSettingsAsset? lodSettingsAsset,
            LandscapeData? landscapeData)
        {
            if (realmPartitionSettings != null)
                this.realmPartitionSettings = realmPartitionSettings;

            if (lodSettingsAsset != null)
                this.lodSettingsAsset = lodSettingsAsset;

            if (landscapeData != null)
                this.landscapeData = landscapeData;
        }

        public void SetActive(bool active) { }

        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate) { }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            SetSceneLoadRadius(preset.environmentSettings.sceneLoadRadius);
            sceneLoadRadius.Value = preset.environmentSettings.sceneLoadRadius;

            SetLodThreshold(preset.environmentSettings.lod1Threshold, 0);
            lod1Threshold.Value = preset.environmentSettings.lod1Threshold;

            SetLodThreshold(preset.environmentSettings.lod2Threshold, 1);
            lod2Threshold.Value = preset.environmentSettings.lod2Threshold;

            SetTerrainLodBias(preset.environmentSettings.terrainLODBias);
            this.terrainLODBias.Value = preset.environmentSettings.terrainLODBias;

            SetDetailDensity(preset.environmentSettings.detailDensity);
            this.detailDensity.Value = preset.environmentSettings.detailDensity;

            SetGrassDistance(preset.environmentSettings.grassDistance);
            this.grassDistance.Value = preset.environmentSettings.grassDistance;

            SetChunkCullDistance(preset.environmentSettings.chunkCullDistance);
            this.chunkCullDistance.Value = preset.environmentSettings.chunkCullDistance;
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            sceneLoadRadius = PersistentSetting.CreateInt("SceneLoadRadius", currentPreset.environmentSettings.sceneLoadRadius);
            lod1Threshold = PersistentSetting.CreateInt("LOD1Threshold", currentPreset.environmentSettings.lod1Threshold);
            lod2Threshold = PersistentSetting.CreateInt("LOD2Threshold", currentPreset.environmentSettings.lod2Threshold);
            terrainLODBias = PersistentSetting.CreateFloat("TerrainLODBias", currentPreset.environmentSettings.terrainLODBias);
            detailDensity = PersistentSetting.CreateFloat("DetailDensity", currentPreset.environmentSettings.detailDensity);
            grassDistance = PersistentSetting.CreateFloat("GrassDistance", currentPreset.environmentSettings.grassDistance);
            chunkCullDistance = PersistentSetting.CreateFloat("ChunkCullDistance", currentPreset.environmentSettings.chunkCullDistance);

            // Apply settings
            SetSceneLoadRadius(sceneLoadRadius.Value);
            SetLodThreshold(lod1Threshold.Value, 0);
            SetLodThreshold(lod2Threshold.Value, 1);
            SetTerrainLodBias(terrainLODBias.Value);
            SetDetailDensity(detailDensity.Value);
            SetGrassDistance(grassDistance.Value);
            SetChunkCullDistance(chunkCullDistance.Value);
        }

        private void SetSceneLoadRadius(int maxLoadingDistanceInParcels)
        {
            if (realmPartitionSettings == null)
                return;

            realmPartitionSettings.MaxLoadingDistanceInParcels = maxLoadingDistanceInParcels;
        }

        private void SetLodThreshold(int lodThreshold, int index)
        {
            if (lodSettingsAsset == null)
                return;

            lodSettingsAsset.LodPartitionBucketThresholds[index] = lodThreshold;
        }

        private void SetTerrainLodBias(float lodBias)
        {
            float tempLodBias = UNITY_DEFAULT_LOD_BIAS * lodBias / 100f;
            if (!(Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f))
                return;

            QualitySettings.lodBias = tempLodBias;
        }

        private void SetDetailDensity(float density)
        {
            float tempDensity = density / 100f;
            if (!(Math.Abs(QualitySettings.terrainDetailDensityScale - tempDensity) > 0.005f))
                return;

            QualitySettings.terrainDetailDensityScale = tempDensity;
        }

        private void SetGrassDistance(float distance)
        {
            if (!(Math.Abs(QualitySettings.terrainDetailDistance - distance) > 0.005f))
                return;

            QualitySettings.terrainDetailDistance = distance;
        }

        private void SetChunkCullDistance(float distance)
        {
            if (landscapeData == null)
                return;

            landscapeData.DetailDistance = distance;
        }
    }
}
