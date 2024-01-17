using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
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
        private readonly int sceneLODLimit;

        public static int updater;

        public ResolveVisualSceneStateSystem(World world, int sceneLODLimit) : base(world)
        {
            this.sceneLODLimit = sceneLODLimit;
            updater = 0;
        }

        protected override void Update(float t)
        {
            UpdateVisualStateQuery(World);
            AddSceneVisualStateQuery(World);
            UpdateVisualStateQuery(World);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void AddSceneVisualState(in Entity entity, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            VisualSceneState visualSceneState = new VisualSceneState();
            ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
            //We mark it as clean, so it can be grabbed by the ResolveSceneStateByIncreasingRadiusSystem and not the UpdateVisualSceneStateSystem
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisualState(ref VisualSceneState visualSceneState, ref PartitionComponent partition,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (partition.IsDirty)
                ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
        }

        private void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent)
        {
            //If the scene is empty, no lods are possible
            if (sceneDefinitionComponent.IsEmpty)
            {
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
            }
            else
            {
                var candidateState = partition.Bucket <= sceneLODLimit
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                {
                    updater++;
                    //Debug.Log($"JUANI UPDATING VISUAL STATE {partition.Bucket} {candidateState} {updater}");
                }
                if (candidateState != visualSceneState.CurrentVisualSceneState)
                {
                    visualSceneState.CurrentVisualSceneState = candidateState;
                    visualSceneState.IsDirty = true;
                }
            }

        }

    }
}