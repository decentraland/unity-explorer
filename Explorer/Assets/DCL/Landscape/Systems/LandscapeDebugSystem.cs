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
using TerrainData = Decentraland.Terrain.TerrainData;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeDebugSystem : BaseUnityLoopSystem
    {
        private readonly SatelliteFloor floor;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly LandscapeData landscapeData;
        private readonly TerrainData terrainData;
        private readonly ElementBinding<int> lodBias;
        private readonly ElementBinding<int> detailDensity;
        private readonly ElementBinding<int> detailDistance;
        private readonly ElementBinding<int> environmentDistance;

        private int lastEnvironmentDistanceApplied;

        private LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder,
            SatelliteFloor floor, RealmPartitionSettingsAsset realmPartitionSettings,
            LandscapeData landscapeData, TerrainData terrainData) : base(world)
        {
            this.floor = floor;
            this.realmPartitionSettings = realmPartitionSettings;
            this.landscapeData = landscapeData;
            this.terrainData = terrainData;

            lodBias = new ElementBinding<int>(180);
            detailDensity = new ElementBinding<int>(100);
            detailDistance = new ElementBinding<int>(80);
            environmentDistance = new ElementBinding<int>((int)landscapeData.EnvironmentDistance);
            lastEnvironmentDistanceApplied = (int)landscapeData.EnvironmentDistance;

            debugBuilder.TryAddWidget("Landscape")
                        ?.AddIntFieldWithConfirmation(realmPartitionSettings.MaxLoadingDistanceInParcels, "Set Load Radius", OnLoadRadiusConfirm)
                        .AddIntSliderField("LOD bias %", lodBias, 1, 250)
                        .AddIntSliderField("Detail Density %", detailDensity, 0, 100)
                        .AddIntSliderField("Detail Distance", detailDistance, 0, 300)
                        .AddIntSliderField("Environment Distance", environmentDistance, 1, 10000)
                        .AddToggleField("Ground", OnTerrainToggle, terrainData.RenderGround)
                        .AddToggleField("Trees and Detail", OnDetailToggle, terrainData.RenderTreesAndDetail)
                        .AddToggleField("Satellite", OnSatelliteToggle, landscapeData.showSatelliteView);
        }

        private void OnTerrainToggle(ChangeEvent<bool> evt)
        {
            terrainData.RenderGround = evt.newValue;
        }

        private void OnDetailToggle(ChangeEvent<bool> evt)
        {
            terrainData.RenderTreesAndDetail = evt.newValue;
        }

        private void OnSatelliteToggle(ChangeEvent<bool> evt)
        {
            floor.SwitchVisibilitySetting(evt.newValue);
        }

        private void OnLoadRadiusConfirm(int value)
        {
            realmPartitionSettings.MaxLoadingDistanceInParcels = value;
        }

        protected override void Update(float t)
        {
            float tempLodBias = lodBias.Value / 100f;
            float tempDensity = detailDensity.Value / 100f;
            int tempDistance = detailDistance.Value;

            if (Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f)
                QualitySettings.lodBias = tempLodBias;

            if (Math.Abs(QualitySettings.terrainDetailDensityScale - tempDensity) > 0.005f)
                QualitySettings.terrainDetailDensityScale = tempDensity;

            if (Math.Abs(QualitySettings.terrainDetailDistance - tempDistance) > 0.005f)
                QualitySettings.terrainDetailDistance = tempDistance;

            if (lastEnvironmentDistanceApplied != environmentDistance.Value)
            {
                landscapeData.EnvironmentDistance = environmentDistance.Value;
                lastEnvironmentDistanceApplied = environmentDistance.Value;
            }
        }
    }
}
