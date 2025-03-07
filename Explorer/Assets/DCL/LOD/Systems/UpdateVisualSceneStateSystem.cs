﻿using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using Arch.System;
using ECS.Unity.Transforms.Components;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveVisualSceneStateSystem))]
    [UpdateAfter(typeof(PartitionSceneEntitiesSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly ILODCache lodCache;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly VisualSceneStateResolver visualSceneStateResolver;

        internal UpdateVisualSceneStateSystem(World world, IRealmData realmData, IScenesCache scenesCache, ILODCache lodCache,
            ILODSettingsAsset lodSettingsAsset, VisualSceneStateResolver visualSceneStateResolver) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.visualSceneStateResolver = visualSceneStateResolver;
        }

        protected override void Update(float t)
        {
            UpdateVisualSceneStateQuery(World);

            CheckLODToPromiseQuery(World);
            CheckSceneToLODQuery(World);
            CheckPromiseToLODQuery(World);

            CleanSceneLODSharedStateQuery(World);
            CleanPromiseLODSharedStateQuery(World);
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        [All(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void CheckPromiseToLOD(in Entity entity, ref VisualSceneState visualSceneState)
        {
            if (!visualSceneState.IsDirty) return;
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE) return;

            visualSceneState.IsDirty = false;
            World.Add(entity, SceneLODInfo.Create());
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        [All(typeof(ISceneFacade))]
        private void CheckSceneToLOD(in Entity entity, ref VisualSceneState visualSceneState)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE) return;

            visualSceneState.IsDirty = false;
            World.Add(entity, SceneLODInfo.Create());
        }


        [Query]
        [All(typeof(SceneLODInfo))]
        [None(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void CheckLODToPromise(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD) return;

            visualSceneState.IsDirty = false;

            World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                partitionComponent));
        }

        [Query]
        private void CleanSceneLODSharedState(in Entity entity, ref SceneLODInfo sceneLODInfo,
            ref ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref VisualSceneState visualSceneState)
        {
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                if (sceneFacade.IsSceneReady())
                {
                    sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels,
                        lodCache,
                        World);
                    World.Remove<SceneLODInfo>(entity);
                }
            }
            else if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                //Dispose scene
                sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache,
                    sceneDefinitionComponent.Parcels);
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        [All(typeof(SceneLODInfo))]
        private void CleanPromiseLODSharedState(in Entity entity,
            ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise,
            ref VisualSceneState visualSceneState)
        {
            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                //Dispose promise
                promise.ForgetLoading(World);
                World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        private void UpdateVisualSceneState(ref PartitionComponent partitionComponent,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref VisualSceneState visualSceneState)
        {
            if (partitionComponent.IsDirty && !sceneDefinitionComponent.IsPortableExperience) // Visual State is never changed for Portable Experiences
                visualSceneStateResolver.ResolveVisualSceneState(ref visualSceneState, partitionComponent,
                    sceneDefinitionComponent, lodSettingsAsset, realmData);
        }
    }
}
