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
        private readonly int bucketSceneLodLimit;
        private readonly Vector2Int[] bucketLodsLimits;
        private readonly IComponentPool<Transform> transformPool;

        /// <summary>
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketSceneLodLimit">Bucket until we render a scene</param>
        /// <param name="bucketLodLimits">
        ///     Array that controls bucket lod handling. X represents inferior limit and exclusive, Y
        ///     represents upper limit and inclusive
        /// </param>
        /// <param name="transformPool">Transform pool</param>
        public ResolveVisualSceneStateSystem(World world, int bucketSceneLodLimit, Vector2Int[] bucketLodLimits,
            IComponentPool<Transform> transformPool) :
            base(world)
        {
            this.bucketSceneLodLimit = bucketSceneLodLimit;
            bucketLodsLimits = bucketLodLimits;
            this.transformPool = transformPool;
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
            ResolveVisualSceneState(ref visualSceneState, partition);
            visualSceneState.IsDirty = true;
            World.Add(entity, visualSceneState);
        }
        
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisualState(ref VisualSceneState visualSceneState, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.IsEmpty) return; //We dont update visuals state of empty scenes
            if (!partition.IsDirty) return;

            ResolveVisualSceneState(ref visualSceneState, partition);
        }

        private void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition)
        {
            var candidateState = partition.Bucket <= bucketSceneLodLimit ? VisualSceneStateEnum.SHOWING_SCENE : VisualSceneStateEnum.SHOWING_LOD;
            visualSceneState.IsDirty = candidateState != visualSceneState.CurrentVisualSceneState;
            visualSceneState.CurrentVisualSceneState = candidateState;
        }

        /*[Query]
        [None(typeof(DeleteEntityIntention), typeof(ISceneFacade), typeof(VisualSceneState))]
        private void ResolveSceneFacadePromise(in Entity entity,
            ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref PartitionComponent partition,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            // Gracefully consume with the possibility of repetitions (in case the scene loading has failed)
            if (promise.IsConsumed) return;

            if (promise.TryConsume(World, out var result) && result.Succeeded)
            {
                var scene = result.Asset;
                var visualSceneState = new VisualSceneState
                {
                    IsDirty = true
                };

                if (sceneDefinitionComponent.IsEmpty)
                {
                    visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
                    //TODO: Empty scene lod. Not completely happy on how Im doing it, but I need it for the removal of the scene
                    World.Add(entity, scene, visualSceneState, new SceneLOD());
                }
                else
                {
                    if (partition.Bucket <= bucketSceneLodLimit)
                        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
                    else
                        visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;

                    //We initialize LODS
                    var sceneLOD = new SceneLOD(sceneDefinitionComponent, bucketLodsLimits, transformPool);
                    sceneLOD.LoadLod();

                    World.Add(entity, scene, visualSceneState, sceneLOD);
                }
            }
        }*/

 
        
    }
}