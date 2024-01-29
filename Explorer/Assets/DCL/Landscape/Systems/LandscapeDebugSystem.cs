using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
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
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly LandscapeData landscapeData;

        public LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder, RealmPartitionSettingsAsset realmPartitionSettings, LandscapeData landscapeData) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;
            this.landscapeData = landscapeData;

            debugBuilder.AddWidget("Landscape")
                        .AddIntFieldWithConfirmation(realmPartitionSettings.MaxLoadingDistanceInParcels, "Set Load Radius", OnLoadRadiusConfirm)
                        .AddToggleField("Hide Satellite View", OnSatelliteViewToggle, false);
        }

        private void OnSatelliteViewToggle(ChangeEvent<bool> evt)
        {
            landscapeData.disableSatelliteView = evt.newValue;
            Debug.Log(landscapeData.disableSatelliteView);
        }

        private void OnLoadRadiusConfirm(int value)
        {
            realmPartitionSettings.MaxLoadingDistanceInParcels = value;
        }

        protected override void Update(float t) { }
    }
}
