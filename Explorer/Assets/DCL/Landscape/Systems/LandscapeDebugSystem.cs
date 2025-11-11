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
        private readonly SatelliteFloor floor;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly global::Global.Dynamic.Landscapes.Landscape landscape;
        private readonly LandscapeData landscapeData;
        private readonly ElementBinding<int> lodBias;
        private readonly ElementBinding<int> cullDistance;

        private int lastCullDistanceApplied;

        private LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder,
            SatelliteFloor floor, RealmPartitionSettingsAsset realmPartitionSettings,
            global::Global.Dynamic.Landscapes.Landscape landscape, LandscapeData landscapeData) : base(world)
        {
            this.floor = floor;
            this.realmPartitionSettings = realmPartitionSettings;
            this.landscape = landscape;
            this.landscapeData = landscapeData;

            lodBias = new ElementBinding<int>(180);
            cullDistance = new ElementBinding<int>((int)landscapeData.DetailDistance);
            lastCullDistanceApplied = (int)landscapeData.DetailDistance;

            debugBuilder.TryAddWidget("Landscape")
                       ?.AddIntFieldWithConfirmation(realmPartitionSettings.MaxLoadingDistanceInParcels,
                             "Set Load Radius", OnLoadRadiusConfirm)
                        .AddIntSliderField("LOD bias %", lodBias, 50, 250)
                        .AddIntSliderField("Draw Distance", cullDistance, 12, 7250)
                        .AddToggleField("Ground", OnGroundToggle, landscapeData.RenderGround)
                        .AddToggleField("Trees", OnTreesToggle, landscapeData.RenderTrees)
                        .AddToggleField("Grass", OnGrassToggle, landscapeData.RenderGrass)
                        .AddToggleField("Satellite", OnSatelliteToggle, landscapeData.ShowSatelliteFloor);
        }

        private void OnGroundToggle(ChangeEvent<bool> evt) =>
            landscapeData.RenderGround = evt.newValue;

        private void OnTreesToggle(ChangeEvent<bool> evt)
        {
            landscapeData.RenderTrees = evt.newValue;
            TreeData? trees = landscape.CurrentTerrain.Trees;

            if (trees == null)
                return;

            if (evt.newValue)
                trees.Show();
            else
                trees.Hide();
        }

        private void OnGrassToggle(ChangeEvent<bool> evt) =>
            landscapeData.RenderGrass = evt.newValue;

        private void OnSatelliteToggle(ChangeEvent<bool> evt) =>
            floor.SwitchVisibilitySetting(evt.newValue);

        private void OnLoadRadiusConfirm(int value) =>
            realmPartitionSettings.MaxLoadingDistanceInParcels = value;

        protected override void Update(float t)
        {
            float tempLodBias = lodBias.Value / 100f;

            if (Math.Abs(QualitySettings.lodBias - tempLodBias) > 0.005f)
                QualitySettings.lodBias = tempLodBias;

            if (lastCullDistanceApplied != cullDistance.Value)
            {
                landscapeData.DetailDistance = cullDistance.Value;
                lastCullDistanceApplied = cullDistance.Value;
            }
        }
    }
}
