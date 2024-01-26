using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateBefore(typeof(UpdateVisualSceneStateSystem))]
    [UpdateBefore(typeof(UpdateSceneLODInfoSystem))]
    public partial class ResolveVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly ProvidedAsset<LODSettingsAsset> lodSettingsAsset;

        public ResolveVisualSceneStateSystem(World world, ProvidedAsset<LODSettingsAsset> lodSettingsAsset) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
        }

        protected override void Update(float t)
        {
            AddSceneVisualStateQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void AddSceneVisualState(in Entity entity, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            VisualSceneState visualSceneState = new VisualSceneState();
            VisualSceneStateUtils.ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent, lodSettingsAsset);
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }

    }
}
