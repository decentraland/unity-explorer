using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization;
using System;
using UnityEngine;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeDebugSystem : BaseUnityLoopSystem
    {
        private const float UNITY_DEFAULT_LOD_BIAS = 0.8f;
        private readonly RealmPartitionSettingsAsset realmPartitionSettings;
        private readonly ElementBinding<int> lodBias;
        private readonly ElementBinding<int> detailDensity;
        private readonly ElementBinding<int> detailDistance;

        public LandscapeDebugSystem(World world, IDebugContainerBuilder debugBuilder, RealmPartitionSettingsAsset realmPartitionSettings) : base(world)
        {
            this.realmPartitionSettings = realmPartitionSettings;

            lodBias = new ElementBinding<int>(100);
            detailDensity = new ElementBinding<int>(100);
            detailDistance = new ElementBinding<int>(80);

            debugBuilder.AddWidget("Landscape")
                        .AddIntFieldWithConfirmation(realmPartitionSettings.MaxLoadingDistanceInParcels, "Set Load Radius", OnLoadRadiusConfirm)
                        .AddIntSliderField("LOD bias %", lodBias, 1, 150)
                        .AddIntSliderField("Detail Density %", detailDensity, 0, 100)
                        .AddIntSliderField("Detail Distance", detailDistance, 0, 300);
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
        }
    }
}
