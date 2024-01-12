using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Realm;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveVisualSceneStateSystem : BaseUnityLoopSystem
    {
        public ResolveVisualSceneStateSystem(World world) : base(world)
        {
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
            //On creation, the decision to be add a SceneLodInfo or an Scene Promise is done in ResolveSceneStateByIncreasingRadiusSystem. 
            //So this should not be dirty at this point
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisualState(ref VisualSceneState visualSceneState, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.Definition.id.Equals(
                    "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                Debug.Log("JUANI IM STILL IN UPDATE VISUAL STATE " + partition.Bucket);
            if (!partition.IsDirty) return;

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
                var candidateState = partition.Bucket <= VisualSceneStateConstants.SCENE_BUCKET_LIMIT
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;
                visualSceneState.IsDirty = candidateState != visualSceneState.CurrentVisualSceneState;
                visualSceneState.CurrentVisualSceneState = candidateState;
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log($"JUANI {visualSceneState.IsDirty} {visualSceneState.CurrentVisualSceneState}");
            }
        }

    }
}