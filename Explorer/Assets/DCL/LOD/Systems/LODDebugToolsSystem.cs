using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Arch.System;
using DCL.DebugUtilities.UIBindings;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

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

        private readonly Transform missingSceneParent;

        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset, Transform missingSceneParent) : base(world)
        {
            this.debugBuilder = debugBuilder;
            this.lodSettingsAsset = lodSettingsAsset;
            this.missingSceneParent = missingSceneParent;
            lodSettingsAsset.IsColorDebuging = false;
            debugBuilder.AddWidget("LOD")
                .AddSingleButton("LOD Debugging", ToggleLODColor)
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[0], "LOD 1 Threshold",  newValue => SetLOD(newValue, 0))
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[1], "LOD 2 Threshold",  newValue => SetLOD(newValue, 1))
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[2], "LOD 3 Threshold",  newValue => SetLOD(newValue, 2));
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
        private void AddSceneLODInfoDebug(in Entity entity)
        {
            World.Add(entity, SceneLODInfoDebug.Create(missingSceneParent));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void UnloadSceneLODInfoDebug(in Entity entity, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            sceneLODInfoDebug.Dispose();
            World.Remove<SceneLODInfoDebug>(entity);
        }

        [Query]
        private void UpdateLODDebugInfo(ref SceneDefinitionComponent sceneDefinitionComponent, ref SceneLODInfo sceneLODInfo, ref SceneLODInfoDebug sceneLODInfoDebug)
        {
            if (sceneLODInfo.GetCurrentLOD() == null) return;

            var lodAsset = sceneLODInfo.GetCurrentLOD()!.Value;
            if (!lodAsset.LodKey.Level.Equals(sceneLODInfoDebug.CurrentLODLevel))
                sceneLODInfoDebug.Update(sceneDefinitionComponent.Definition.metadata.scene.DecodedParcels, lodAsset, lodSettingsAsset);
        }
    }

}
