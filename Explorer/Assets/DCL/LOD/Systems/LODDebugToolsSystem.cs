using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using System;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class LODDebugToolsSystem : BaseUnityLoopSystem
    {
        private IDebugContainerBuilder debugBuilder;
        private readonly ILODSettingsAsset lodSettingsAsset;

        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset) : base(world)
        {
            this.debugBuilder = debugBuilder;
            this.lodSettingsAsset = lodSettingsAsset;
            lodSettingsAsset.IsColorDebuging = false;

            debugBuilder.AddWidget("LOD")
                        .AddSingleButton("Toggle lod color", ToggleLODColor)
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[0], "LOD 1 Threshold", SetLOD1)
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[1], "LOD 2 Threshold", SetLOD2);
        }

        private void SetLOD1(int value)
        {
            lodSettingsAsset.LodPartitionBucketThresholds[0] = value;
        }

        private void SetLOD2(int value)
        {
            lodSettingsAsset.LodPartitionBucketThresholds[1] = value;
        }

        private void ToggleLODColor()
        {
            lodSettingsAsset.IsColorDebuging = !lodSettingsAsset.IsColorDebuging;

            World.Query(in new QueryDescription().WithAll<SceneLODInfo>(),
                (ref SceneLODInfo sceneLODInfo) => { sceneLODInfo.ToggleDebugColors(); });
        }

        protected override void Update(float t) { }
    }
}
