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
            .WithAll<SceneLODInfoDebug>();

        private readonly Transform missingSceneParent;

        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset, Transform missingSceneParent) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            this.missingSceneParent = missingSceneParent;
            lodSettingsAsset.IsColorDebuging = false;

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
                UnloadSceneLODInfoDebugQuery(World);
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
        private void AddSceneLODInfoDebug(in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            World.Add(entity, SceneLODInfoDebug.Create(missingSceneParent, lodSettingsAsset, sceneDefinitionComponent.Definition.metadata.scene.DecodedParcels));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadSceneLODInfoDebug(in Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            sceneLODInfoDebug.Dispose();
            World.Remove<SceneLODInfoDebug>(entity);
        }

        [Query]
        private void UpdateLODDebugInfo(ref SceneLODInfo sceneLODInfo, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            // if (sceneLODInfo.CurrentLOD == null) return;
            //
            // var lodAsset = sceneLODInfo.CurrentLOD;
            // if (lodAsset.LodKey.Level != sceneLODInfoDebug.CurrentLODLevel || lodAsset.State != sceneLODInfoDebug.CurrentLODState)
            //     sceneLODInfoDebug.Update(lodAsset);
        }
    }

}
