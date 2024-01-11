using System.Threading;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Realm;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByRadiusSystem))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class ResolveSceneVisualStateSystem : BaseUnityLoopSystem
    {
        private readonly int bucketSceneLodLimit;
        private readonly Vector2Int[] bucketLodsLimits;

        /// <summary>
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bucketSceneLodLimit">Bucket until we render a scene</param>
        /// <param name="bucketLodLimits">
        ///     Array that controls bucket lod handling. X represents inferior limit and exclusive, Y
        ///     represents upper limit and inclusive
        /// </param>
        public ResolveSceneVisualStateSystem(World world, int bucketSceneLodLimit, Vector2Int[] bucketLodLimits) :
            base(world)
        {
            this.bucketSceneLodLimit = bucketSceneLodLimit;
            bucketLodsLimits = bucketLodLimits;
        }

        protected override void Update(float t)
        {
            ResolveSceneFacadePromiseQuery(World);
            UpdateVisualStateQuery(World);
        }

        [Query]
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
                    isDirty = true
                };

                visualSceneState.currentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;

                if (partition.Bucket <= bucketSceneLodLimit)
                    visualSceneState.currentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
                else
                    visualSceneState.currentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;

                //We initialize LODS
                var sceneLOD = new SceneLOD(sceneDefinitionComponent, bucketLodsLimits);
                sceneLOD.LoadLod();

                World.Add(entity, scene, visualSceneState, sceneLOD);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVisualState(in Entity entity, ref VisualSceneState visualSceneState,
            ref PartitionComponent partition, ref SceneLOD sceneLOD)
        {
            if (!partition.IsDirty) return;

            if (partition.Bucket <= bucketSceneLodLimit &&
                visualSceneState.currentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD))
            {
                visualSceneState.currentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
                sceneLOD.Dispose();
            }
            else if (partition.Bucket > bucketSceneLodLimit &&
                     visualSceneState.currentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_SCENE))
            {
                visualSceneState.currentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
                World.Add<UnloadRunningSceneIntention>(entity);
            }

            visualSceneState.isDirty = true;
        }
    }
}