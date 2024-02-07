using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Arch.System;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class LODDebugToolsSystem : BaseUnityLoopSystem
    {
        private IDebugContainerBuilder debugBuilder;
        private readonly ILODSettingsAsset lodSettingsAsset;

        private static readonly QueryDescription REMOVE_QUERY = new QueryDescription()
            .WithAll<SceneLODInfoDebug>();

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

            if (!lodSettingsAsset.IsColorDebuging)
                World.Query(REMOVE_QUERY,
                    (Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug) =>
                    {
                        sceneLODInfoDebug.Dispose();
                        World.Remove<SceneLODInfoDebug>(entity);
                    });
        }

        protected override void Update(float t)
        {
            if (lodSettingsAsset.IsColorDebuging)
            {
                AddSceneLODInfoDebugQuery(World);
                RemoveSceneLODInfoQuery(World);
                UpdateLODDebugInfoQuery(World);
            }
        }

        [Query]
        [All(typeof(SceneLODInfoDebug))]
        [None(typeof(SceneLODInfo))]
        private void RemoveSceneLODInfo(in Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            sceneLODInfoDebug.Dispose();
            World.Remove<SceneLODInfoDebug>(entity);
        }

        [Query]
        [All(typeof(SceneLODInfo))]
        [None(typeof(SceneLODInfoDebug))]
        private void AddSceneLODInfoDebug(in Entity entity)
        {
            World.Add(entity, SceneLODInfoDebug.Create());
        }

        [Query]
        private void UpdateLODDebugInfo(ref SceneLODInfo sceneLODInfo, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            if (sceneLODInfo.CurrentLOD == null) return;

            var lodAsset = sceneLODInfo.CurrentLOD.Value;
            if (lodAsset.LoadingFailed) return;

            if (!lodAsset.LodKey.Level.Equals(sceneLODInfoDebug.CurrentLODLevel))
                sceneLODInfoDebug.Update(sceneLODInfo.CurrentLOD.Value, lodSettingsAsset);
        }
    }
}
