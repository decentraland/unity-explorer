using DCL.DebugUtilities;
using DCL.Landscape.Settings;
using DCL.LOD;
using DCL.Prefs;
using DCL.Rendering.GPUInstancing;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Storage;

namespace DCL.Quality.Runtime
{
    public class EnvironmentSettingsRuntime : IQualitySettingRuntime
    {
        private PersistentSetting<int> maxSimultaneousVideos;
        private PersistentSetting<int> sceneLoadRadius;
        private PersistentSetting<int> lod1Threshold;
        private PersistentSetting<float> terrainLODBias;
        private PersistentSetting<float> detailDensity;
        private PersistentSetting<float> grassDistance;
        private PersistentSetting<float> chunkCullDistance;

        private readonly RealmPartitionSettingsAsset? realmPartitionSettings;
        private readonly VideoPrioritizationSettings? videoPrioritizationSettings;
        private readonly ILODSettingsAsset? lodSettingsAsset;
        private readonly LandscapeData? landscapeData;
        private readonly GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings roadsSettings;

        public bool IsActive => true;

        public EnvironmentSettingsRuntime(
            RealmPartitionSettingsAsset? realmPartitionSettings,
            VideoPrioritizationSettings? videoPrioritizationSettings,
            ILODSettingsAsset? lodSettingsAsset,
            LandscapeData? landscapeData,
            GPUInstancingRenderFeature.GPUInstancingRenderFeature_Settings? roadsSettings
        )
        {
            if (roadsSettings != null) this.roadsSettings = roadsSettings;
            if (realmPartitionSettings != null) this.realmPartitionSettings = realmPartitionSettings;
            if (videoPrioritizationSettings != null) this.videoPrioritizationSettings = videoPrioritizationSettings;
            if (lodSettingsAsset != null) this.lodSettingsAsset = lodSettingsAsset;
            if (landscapeData != null) this.landscapeData = landscapeData;
        }

        public void SetActive(bool active) { }

        public void AddDebugView(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate) { }

        public void ApplyPreset(QualitySettingsAsset.QualityCustomLevel preset)
        {
            SetSceneLoadRadius(preset.environmentSettings.sceneLoadRadius);
            sceneLoadRadius.Value = roadsSettings.RenderDistanceInParcels = preset.environmentSettings.sceneLoadRadius;

            SetLodThreshold(preset.environmentSettings.lod1Threshold, 0);
            lod1Threshold.Value = preset.environmentSettings.lod1Threshold;

            SetTerrainLodBias(preset.environmentSettings.terrainLODBias);
            this.terrainLODBias.Value = preset.environmentSettings.terrainLODBias;

            SetDetailDensity(preset.environmentSettings.detailDensity);
            this.detailDensity.Value = preset.environmentSettings.detailDensity;

            SetGrassDistance(preset.environmentSettings.grassDistance);
            this.grassDistance.Value = preset.environmentSettings.grassDistance;

            SetChunkCullDistance(preset.environmentSettings.chunkCullDistance);
            this.chunkCullDistance.Value = preset.environmentSettings.chunkCullDistance;

            SetMaxSimultaneousVideos(preset.environmentSettings.maxSimultaneousVideos);
            this.maxSimultaneousVideos.Value = preset.environmentSettings.maxSimultaneousVideos;
        }

        public void RestoreState(QualitySettingsAsset.QualityCustomLevel currentPreset)
        {
            sceneLoadRadius = PersistentSetting.CreateInt(DCLPrefKeys.PS_SCENE_LOAD_RADIUS, currentPreset.environmentSettings.sceneLoadRadius).WithSetForceDefaultValue();
            lod1Threshold = PersistentSetting.CreateInt(DCLPrefKeys.PS_LOD1_THRESHOLD, currentPreset.environmentSettings.lod1Threshold).WithSetForceDefaultValue();
            terrainLODBias = PersistentSetting.CreateFloat(DCLPrefKeys.PS_TERRAIN_LOD_BIAS, currentPreset.environmentSettings.terrainLODBias).WithSetForceDefaultValue();
            detailDensity = PersistentSetting.CreateFloat(DCLPrefKeys.PS_DETAIL_DENSITY, currentPreset.environmentSettings.detailDensity).WithSetForceDefaultValue();
            grassDistance = PersistentSetting.CreateFloat(DCLPrefKeys.PS_GRASS_DISTANCE, currentPreset.environmentSettings.grassDistance).WithSetForceDefaultValue();
            chunkCullDistance = PersistentSetting.CreateFloat(DCLPrefKeys.PS_CHUNK_CULL_DISTANCE, currentPreset.environmentSettings.chunkCullDistance).WithSetForceDefaultValue();
            maxSimultaneousVideos = PersistentSetting.CreateInt(DCLPrefKeys.PS_MAX_SIMULTANEOUS_VIDEOS, currentPreset.environmentSettings.maxSimultaneousVideos).WithSetForceDefaultValue();

            // Apply settings
            SetSceneLoadRadius(sceneLoadRadius.Value);
            SetLodThreshold(lod1Threshold.Value, 0);
            SetTerrainLodBias(terrainLODBias.Value);
            SetDetailDensity(detailDensity.Value);
            SetGrassDistance(grassDistance.Value);
            SetChunkCullDistance(chunkCullDistance.Value);
            SetMaxSimultaneousVideos(maxSimultaneousVideos.Value);
        }

        private void SetSceneLoadRadius(int maxLoadingDistanceInParcels)
        {
            if (realmPartitionSettings == null)
                return;

            realmPartitionSettings.MaxLoadingDistanceInParcels = maxLoadingDistanceInParcels;
            roadsSettings.RenderDistanceInParcels = maxLoadingDistanceInParcels;
        }

        private void SetMaxSimultaneousVideos(int maxSimultaneousVideos)
        {
            if(videoPrioritizationSettings == null)
                return;

            videoPrioritizationSettings.MaximumSimultaneousVideos = maxSimultaneousVideos;
        }

        private void SetLodThreshold(int lodThreshold, int index)
        {
            if (lodSettingsAsset == null)
                return;

            lodSettingsAsset.LodPartitionBucketThresholds[index] = lodThreshold;
        }

        private static void SetTerrainLodBias(float lodBias)
        {
            float tempLodBias = lodBias / 100f;
            if (!(Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f))
                return;

            QualitySettings.lodBias = tempLodBias;
        }

        private static void SetDetailDensity(float density)
        {
            float tempDensity = density / 100f;
            if (!(Math.Abs(QualitySettings.terrainDetailDensityScale - tempDensity) > 0.005f))
                return;

            QualitySettings.terrainDetailDensityScale = tempDensity;
        }

        private static void SetGrassDistance(float distance)
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
