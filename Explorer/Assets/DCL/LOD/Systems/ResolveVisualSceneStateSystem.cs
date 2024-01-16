using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD.Components;
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
    public partial class ResolveVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly int sceneLODLimit;

        public ResolveVisualSceneStateSystem(World world, int sceneLODLimit) : base(world)
        {
            this.sceneLODLimit = sceneLODLimit;
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
            //No need to left it dirty. Is going to get grabbed by ResolveSceneStateByIncreasingRadiusSystem
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        [Any(typeof(SceneLODInfo), typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
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
                if (candidateState != visualSceneState.CurrentVisualSceneState)
                {
                    visualSceneState.CurrentVisualSceneState = candidateState;
                    visualSceneState.IsDirty = true;
                }
            }

        }

    }
}