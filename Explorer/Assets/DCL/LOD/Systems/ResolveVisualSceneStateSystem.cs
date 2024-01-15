using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
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
            AddSceneVisualStateQuery(World);
            UpdateVisualStateQuery(World);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void AddSceneVisualState(in Entity entity, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            VisualSceneState visualSceneState = new VisualSceneState();
            ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
            World.Add(entity, visualSceneState);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisualState(ref VisualSceneState visualSceneState, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            //if (sceneDefinitionComponent.Definition.id.Equals(
            //        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
            //    Debug.Log("JUANI IM STILL IN UPDATE VISUAL STATE " + partition.Bucket);

            // TODO: This avoids checking ti twice when components are added in ResolveVisualSceneStateByIncreasingRadiusSystem
            if (visualSceneState.IsDirty)
            {
                ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent);
                return;
            }

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
                visualSceneState.IsDirty = false;
            }
            else
            {
                var candidateState = partition.Bucket <= sceneLODLimit
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;
                visualSceneState.IsDirty = candidateState != visualSceneState.CurrentVisualSceneState;
                visualSceneState.CurrentVisualSceneState = candidateState;
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log($"JUANI {visualSceneState.IsDirty} {visualSceneState.CurrentVisualSceneState}");
            }

            visualSceneState.IsDirty = false;
        }

    }
}