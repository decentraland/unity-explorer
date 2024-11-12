using Arch.Core;
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
using SceneRunner;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
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
        private readonly SceneAssetLock sceneAssetLock;
        private readonly VisualSceneStateResolver visualSceneStateResolver;

        internal UpdateVisualSceneStateSystem(World world, IRealmData realmData, IScenesCache scenesCache, ILODCache lodCache,
            ILODSettingsAsset lodSettingsAsset, VisualSceneStateResolver visualSceneStateResolver, SceneAssetLock sceneAssetLock) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.lodCache = lodCache;
            this.lodSettingsAsset = lodSettingsAsset;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.sceneAssetLock = sceneAssetLock;
        }

        protected override void Update(float t)
        {
            UpdateVisualSceneStateQuery(World);
            UpdateSceneToLODQuery(World);
        }

        [Query]
        private void UpdateSceneToLOD(in Entity entity, ref SceneLODInfo sceneLODInfo,
            ref ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneFacade.SceneData.SceneLoadingConcluded)
            {
                sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, lodCache,
                    World);
                World.Remove<SceneLODInfo>(entity);
                if (sceneFacade is SceneRunner.SceneFacade &&
                    sceneFacade.EcsExecutor.World.Has<TransformComponent>(sceneFacade.PersistentEntities.SceneRoot))
                    sceneFacade.EcsExecutor.World.Get<TransformComponent>(sceneFacade.PersistentEntities.SceneRoot)
                        .Transform.gameObject.SetActive(true);
            }
            else
            {
                if (sceneFacade is SceneRunner.SceneFacade &&
                    sceneFacade.EcsExecutor.World.Has<TransformComponent>(sceneFacade.PersistentEntities.SceneRoot))
                    sceneFacade.EcsExecutor.World.Get<TransformComponent>(sceneFacade.PersistentEntities.SceneRoot)
                        .Transform.gameObject.SetActive(false);
            }
        }


        [Query]
        private void UpdateVisualSceneState(in Entity entity, ref PartitionComponent partitionComponent,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref VisualSceneState visualSceneState)
        {
            if (partitionComponent.IsDirty)
                visualSceneStateResolver.ResolveVisualSceneState(ref visualSceneState, partitionComponent,
                    sceneDefinitionComponent, lodSettingsAsset, realmData);

            if (visualSceneState.IsDirty)
            {
                if (visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_SCENE)
                    && World.Has<SceneLODInfo>(entity)
                    && !World.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity))
                {
                    World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                        partitionComponent));
                }
                else if (visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)
                         && World.Has<ISceneFacade>(entity))
                {
                    //Dispose scene
                    World.Get<ISceneFacade>(entity).DisposeSceneFacadeAndRemoveFromCache(scenesCache,
                        sceneDefinitionComponent.Parcels, sceneAssetLock);
                    World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
                    World.Add(entity, SceneLODInfo.Create());
                }
                else if (visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)
                         && World.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity))
                {
                    World.Get<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity).ForgetLoading(World);
                    World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
                    World.Add(entity, SceneLODInfo.Create());
                }

                visualSceneState.IsDirty = false;
            }
        }
    }
}
