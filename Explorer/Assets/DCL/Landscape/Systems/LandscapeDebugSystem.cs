using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using ECS.Abstract;
using ECS.Prioritization;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeDebugSystem : BaseUnityLoopSystem
    {
        private const float UNITY_DEFAULT_LOD_BIAS = 0.8f;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly LandscapeData landscapeData;
        private readonly ElementBinding<int> lodBias;
        private readonly ElementBinding<int> detailDensity;
        private readonly ElementBinding<int> detailDistance;
        private readonly ElementBinding<int> cullDistance;

        private int lastCullDistanceApplied;

        public LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder, RealmPartitionSettingsAsset realmPartitionSettings, LandscapeData landscapeData) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.landscapeData = landscapeData;

            lodBias = new ElementBinding<int>(180);
            detailDensity = new ElementBinding<int>(100);
            detailDistance = new ElementBinding<int>(80);
            cullDistance = new ElementBinding<int>(5000);

            debugBuilder.AddWidget("Landscape")
                        .AddIntFieldWithConfirmation(realmPartitionSettings.MaxLoadingDistanceInParcels, "Set Load Radius", OnLoadRadiusConfirm)
                        .AddIntSliderField("LOD bias %", lodBias, 1, 250)
                        .AddIntSliderField("Detail Density %", detailDensity, 0, 100)
                        .AddIntSliderField("Grass Distance", detailDistance, 0, 300)
                        .AddIntSliderField("Chunk Cull Distance", cullDistance, 1, 10000)
                        .AddToggleField("Terrain", OnTerrainToggle, landscapeData.drawTerrain)
                        .AddToggleField("Details", OnDetailToggle, landscapeData.drawTerrainDetails)
                        .AddToggleField("Satellite", OnSatelliteToggle, landscapeData.showSatelliteView);
        }

        private void OnTerrainToggle(ChangeEvent<bool> evt)
        {
            landscapeData.drawTerrain = evt.newValue;
        }

        private void OnDetailToggle(ChangeEvent<bool> evt)
        {
            landscapeData.drawTerrainDetails = evt.newValue;
        }

        private void OnSatelliteToggle(ChangeEvent<bool> evt)
        {
            landscapeData.showSatelliteView = evt.newValue;
        }

        private void OnLoadRadiusConfirm(int value)
        {
            realmPartitionSettings.MaxLoadingDistanceInParcels = value;
        }

        protected override void Update(float t)
        {
            float tempLodBias = UNITY_DEFAULT_LOD_BIAS * lodBias.Value / 100f;
            float tempDensity = detailDensity.Value / 100f;
            int tempDistance = detailDistance.Value;

            if (Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f)
                QualitySettings.lodBias = tempLodBias;

            if (Math.Abs(QualitySettings.terrainDetailDensityScale - tempDensity) > 0.005f)
                QualitySettings.terrainDetailDensityScale = tempDensity;

            if (Math.Abs(QualitySettings.terrainDetailDistance - tempDistance) > 0.005f)
                QualitySettings.terrainDetailDistance = tempDistance;

            if (lastCullDistanceApplied != cullDistance.Value)
            {
                landscapeData.detailDistance = cullDistance.Value;
                lastCullDistanceApplied = cullDistance.Value;
            }
        }
    }
}
