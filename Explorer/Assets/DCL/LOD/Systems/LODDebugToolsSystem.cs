using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Arch.System;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace DCL.LOD.Systems
{
    
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.LOD)]
    public partial class LODDebugToolsSystem : BaseUnityLoopSystem
    {
        private readonly ILODSettingsAsset lodSettingsAsset;
        private static readonly QueryDescription REMOVE_QUERY = new QueryDescription()
            .WithAll<SceneLODInfoDebug, SceneLODInfo>();

        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            lodSettingsAsset.IsColorDebugging = false;

            var debugWidgetBuilder = debugBuilder.AddWidget("LOD");
            debugWidgetBuilder
                .AddSingleButton("LOD Debugging", ToggleLODColor)
                .AddToggleField("Enable LOD Streaming", evt => lodSettingsAsset.EnableLODStreaming = evt.newValue, lodSettingsAsset.EnableLODStreaming);

            for (int i = 0; i < lodSettingsAsset.LodPartitionBucketThresholds.Length; i++)
            {
                int index = i;
                debugWidgetBuilder
                    .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[i], $"LOD {i + 1} Threshold",  newValue => SetLOD(newValue, index));
            }

            debugWidgetBuilder.AddIntFieldWithConfirmation(lodSettingsAsset.SDK7LodThreshold, "SDK 7 Threshold",  newValue =>
            {
                lodSettingsAsset.SDK7LodThreshold = newValue;
            });
        }

        private void SetLOD(int newValue, int i)
        {
            lodSettingsAsset.LodPartitionBucketThresholds[i] = newValue;
        }

        private void ToggleLODColor()
        {
            lodSettingsAsset.IsColorDebugging = !lodSettingsAsset.IsColorDebugging;

            if (!lodSettingsAsset.IsColorDebugging)
                World.Query(REMOVE_QUERY,
                    (Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug, ref SceneLODInfo sceneLODInfo) =>
                    {
                        if (string.IsNullOrEmpty(sceneLODInfo.id))
                            return;

                        sceneLODInfoDebug.Dispose(sceneLODInfo);
                        World.Remove<SceneLODInfoDebug>(entity);
                    });
        }

        protected override void Update(float t)
        {
            if (lodSettingsAsset.IsColorDebugging)
            {
                AddSceneLODInfoDebugQuery(World);
                UpdateLODDebugInfoQuery(World);
                UnloadSceneLODInfoDebugQuery(World);
            }
        }
     

        [Query]
        [All(typeof(SceneLODInfo))]
        [None(typeof(SceneLODInfoDebug))]
        private void AddSceneLODInfoDebug(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            World.Add(entity, SceneLODInfoDebug.Create(lodSettingsAsset, sceneDefinitionComponent.Definition.metadata.scene.DecodedParcels));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadSceneLODInfoDebug(in Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug, ref SceneLODInfo sceneLODInfo)
        {
            sceneLODInfoDebug.Dispose(sceneLODInfo);
            World.Remove<SceneLODInfoDebug>(entity);
        }

        [Query]
        private void UpdateLODDebugInfo(ref SceneLODInfo sceneLODInfo, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            sceneLODInfoDebug.Update(sceneLODInfo);
        }
    }

}
