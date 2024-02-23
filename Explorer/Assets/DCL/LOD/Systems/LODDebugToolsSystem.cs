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

        private ElementBinding<ulong> LOD_0_Amount;
        private ElementBinding<ulong> LOD_1_Amount;
        private ElementBinding<ulong> LOD_2_Amount;

        private readonly ElementBinding<ulong> faillingAmount;

        private readonly ElementBinding<ulong> [] lodsAmount;
        private readonly Transform missingSceneParent;


        public LODDebugToolsSystem(World world, IDebugContainerBuilder debugBuilder, ILODSettingsAsset lodSettingsAsset, Transform missingSceneParent) : base(world)
        {
            this.debugBuilder = debugBuilder;
            this.lodSettingsAsset = lodSettingsAsset;
            this.missingSceneParent = missingSceneParent;
            lodSettingsAsset.IsColorDebuging = false;

            lodsAmount = new ElementBinding<ulong>[3];
            for (int i = 0; i < 3; i++)
                lodsAmount[i] = new ElementBinding<ulong>(0);
            
            
            debugBuilder.AddWidget("LOD")
                .AddSingleButton("LOD debugging", ToggleLODColor)
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[0], "LOD 1 Threshold", SetLOD1)
                .AddIntFieldWithConfirmation(lodSettingsAsset.LodPartitionBucketThresholds[1], "LOD 2 Threshold", SetLOD2)
                .AddMarker("LOD_0 amount:", lodsAmount[0], DebugLongMarkerDef.Unit.NoFormat)
                .AddMarker("LOD_1 amount:", lodsAmount[1], DebugLongMarkerDef.Unit.NoFormat)
                .AddMarker("LOD_2 amount:", lodsAmount[2], DebugLongMarkerDef.Unit.NoFormat)
                .AddMarker("Failling amount:", faillingAmount = new ElementBinding<ulong>(0), DebugLongMarkerDef.Unit.NoFormat);
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
            for (int i = 0; i < 3; i++)
            {
                lodsAmount[i].Value = 0;
            }

            faillingAmount.Value = 0;

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
            if (sceneLODInfo.CurrentLOD == null) return;

            var lodAsset = sceneLODInfo.CurrentLOD.Value;

            if (!lodAsset.LodKey.Level.Equals(sceneLODInfoDebug.CurrentLODLevel))
                sceneLODInfoDebug.Update(sceneDefinitionComponent, sceneLODInfo.CurrentLOD.Value, lodSettingsAsset, lodsAmount, faillingAmount);
        }
    }

}
