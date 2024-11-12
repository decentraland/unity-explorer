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

            CheckLODToPromiseQuery(World);
            CheckSceneToLODQuery(World);
            CheckPromiseToLODQuery(World);

            CleanSceneToLODQuery(World);
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        private void CheckPromiseToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            if (!visualSceneState.IsDirty) return;

            if (!visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)) return;

            promise.ForgetLoading(World);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            World.Add(entity, SceneLODInfo.Create());
            visualSceneState.IsDirty = false;
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        [All(typeof(ISceneFacade))]
        private void CheckSceneToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref ISceneFacade sceneFacade)
        {
            if (!visualSceneState.IsDirty) return;

            if (!visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_LOD)) return;

            //Dispose scene
            sceneFacade.DisposeSceneFacadeAndRemoveFromCache(scenesCache,
                sceneDefinitionComponent.Parcels, sceneAssetLock);
            World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            World.Add(entity, SceneLODInfo.Create());

            visualSceneState.IsDirty = false;
        }


        [Query]
        [All(typeof(SceneLODInfo))]
        [None(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void CheckLODToPromise(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (!visualSceneState.CurrentVisualSceneState.Equals(VisualSceneStateEnum.SHOWING_SCENE)) return;

            World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                partitionComponent));

            visualSceneState.IsDirty = false;
        }

        [Query]
        private void CleanSceneToLOD(in Entity entity, ref SceneLODInfo sceneLODInfo,
            ref ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneDefinitionComponent.IsEmpty)
            {
                sceneFacade.EcsExecutor.World.Get<TransformComponent>(sceneFacade.PersistentEntities.SceneRoot)
                    .Transform.gameObject.SetActive(sceneFacade.SceneData.SceneLoadingConcluded);

                if (sceneFacade.SceneData.SceneLoadingConcluded)
                {
                    sceneLODInfo.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels,
                        lodCache,
                        World);
                    World.Remove<SceneLODInfo>(entity);
                }
            }
        }

        [Query]
        private void UpdateVisualSceneState(ref PartitionComponent partitionComponent,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref VisualSceneState visualSceneState)
        {
            if (partitionComponent.IsDirty)
                visualSceneStateResolver.ResolveVisualSceneState(ref visualSceneState, partitionComponent,
                    sceneDefinitionComponent, lodSettingsAsset, realmData);
        }
    }
}
