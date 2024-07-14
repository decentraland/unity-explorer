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

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveVisualSceneStateSystem))]
    [UpdateAfter(typeof(PartitionSceneEntitiesSystem))]
    [LogCategory(ReportCategory.LOD)]
    public partial class UpdateVisualSceneStateSystem : BaseUnityLoopSystem
    {
        /// <summary>
        ///     Represents one of the methods in UpdateVisualSceneStateSystem, they should be converted to static ones to avoid closures
        /// </summary>
        private delegate void ContinuationMethod<T>(Entity entity, ref VisualSceneState visualSceneState, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref T switchComponent);
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly ILODAssetsPool lodAssetsPool;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly SceneAssetLock sceneAssetLock;
        private static readonly QueryDescription VISUAL_STATE_SCENE_QUERY = new QueryDescription()
                                                                           .WithAll<VisualSceneState, PartitionComponent, SceneDefinitionComponent>()
                                                                           .WithAny<SceneLODInfo, ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>()
                                                                           .WithNone<DeleteEntityIntention>();

        private readonly ContinuationMethod<ISceneFacade> sceneFacadeToLODContinuation;
        private readonly ContinuationMethod<AssetPromise<ISceneFacade, GetSceneFacadeIntention>> scenePromiseToLODContinuation;
        private readonly ContinuationMethod<SceneLODInfo> sceneLODToScenePromiseContinuation;
        private readonly VisualSceneStateResolver visualSceneStateResolver;

        internal UpdateVisualSceneStateSystem(World world, IRealmData realmData, IScenesCache scenesCache, ILODAssetsPool lodAssetsPool,
            ILODSettingsAsset lodSettingsAsset, VisualSceneStateResolver visualSceneStateResolver, SceneAssetLock sceneAssetLock) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.lodAssetsPool = lodAssetsPool;
            this.lodSettingsAsset = lodSettingsAsset;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.sceneAssetLock = sceneAssetLock;
            sceneFacadeToLODContinuation = SwapSceneFacadeToLOD;
            scenePromiseToLODContinuation = SwapScenePromiseToLOD;
            sceneLODToScenePromiseContinuation = SwapLODToScenePromise;
        }

        protected override void Update(float t)
        {
            UpdateVisualState_SimulateComponentTypeSwitch();
        }

        private void UpdateVisualState_SimulateComponentTypeSwitch()
        {
            // make a query manually
            Query query = World.Query(VISUAL_STATE_SCENE_QUERY);

            // iterate over all archetypes
            // keep in mind it's "any" filter
            foreach (Archetype archetype in query.GetArchetypeIterator())
            {
                if (archetype.EntityCount == 0) continue;

                // Determine to which branch the logic may go (all of them are mutually exclusive)
                // thus we will avoid filtering again in a separate query
                if (archetype.Has<ISceneFacade>())
                    IterateOverOneOf(archetype, sceneFacadeToLODContinuation);
                else if (archetype.Has<SceneLODInfo>())
                    IterateOverOneOf(archetype, sceneLODToScenePromiseContinuation);
                else if (archetype.Has<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>())
                    IterateOverOneOf(archetype, scenePromiseToLODContinuation);
            }
        }

        private void IterateOverOneOf<T>(Archetype archetype, ContinuationMethod<T> continuationMethod)
        {
            Chunk[] chunks = archetype.Chunks;

            for (var i = 0; i < archetype.ChunkCount; i++)
            {
                ref Chunk chunk = ref chunks[i];

                ref Entity entityFirstElement = ref chunk.Entity(0);
                ref VisualSceneState visualscenestateFirstElement = ref chunk.GetFirst<VisualSceneState>();
                ref SceneDefinitionComponent scenedefinitioncomponentFirstElement = ref chunk.GetFirst<SceneDefinitionComponent>();
                ref T customComponentFirstElement = ref chunk.GetFirst<T>();
                ref PartitionComponent partitioncomponentFirstElement = ref chunk.GetFirst<PartitionComponent>();

                foreach (int entityIndex in chunk)
                {
                    ref SceneDefinitionComponent sceneDefinitionComponent = ref Unsafe.Add(ref scenedefinitioncomponentFirstElement, entityIndex);

                    if (sceneDefinitionComponent.IsPortableExperience) continue;

                    ref readonly Entity entity = ref Unsafe.Add(ref entityFirstElement, entityIndex);
                    ref VisualSceneState visualSceneStateComponent = ref Unsafe.Add(ref visualscenestateFirstElement, entityIndex);

                    ref T customComponent = ref Unsafe.Add(ref customComponentFirstElement, entityIndex);
                    ref PartitionComponent partitionComponent = ref Unsafe.Add(ref partitioncomponentFirstElement, entityIndex);

                    if (partitionComponent.IsDirty)
                    {
                        visualSceneStateResolver.ResolveVisualSceneState(ref visualSceneStateComponent, partitionComponent, sceneDefinitionComponent, lodSettingsAsset, realmData);

                        // we call it directly so we avoid an extra query
                        if (visualSceneStateComponent.IsDirty)
                            continuationMethod(entity, ref visualSceneStateComponent, ref sceneDefinitionComponent, ref partitionComponent, ref customComponent);
                    }
                }
            }
        }

        private void SwapScenePromiseToLOD(Entity entity, ref VisualSceneState visualSceneState, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref AssetPromise<ISceneFacade, GetSceneFacadeIntention> switchcomponent)
        {
            if (sceneDefinitionComponent.IsPortableExperience) return;

            if (visualSceneState.CurrentVisualSceneState != VisualSceneStateEnum.SHOWING_LOD) return;

            var sceneLODInfo = SceneLODInfo.Create();

            //Dispose Promise
            switchcomponent.ForgetLoading(World);

            visualSceneState.IsDirty = false;

            World.Add(entity, sceneLODInfo);
            World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
        }

        private void SwapLODToScenePromise(Entity entity, ref VisualSceneState visualSceneState, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref SceneLODInfo switchComponent)
        {
            if (sceneDefinitionComponent.IsPortableExperience) return;

            if (visualSceneState.CurrentVisualSceneState != VisualSceneStateEnum.SHOWING_SCENE) return;

            switchComponent.DisposeSceneLODAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, World);
            visualSceneState.IsDirty = false;

            //Show Scene
            World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                partitionComponent));

            World.Remove<SceneLODInfo>(entity);
        }

        private void SwapSceneFacadeToLOD(Entity entity, ref VisualSceneState visualSceneState, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partitionComponent, ref ISceneFacade switchComponent)
        {
            if (sceneDefinitionComponent.IsPortableExperience) { return; }

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                //Create LODInfo
                var sceneLODInfo = SceneLODInfo.Create();

                //Dispose scene
                switchComponent.DisposeSceneFacadeAndRemoveFromCache(scenesCache, sceneDefinitionComponent.Parcels, sceneAssetLock);

                visualSceneState.IsDirty = false;

                World.Add(entity, sceneLODInfo);
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }
    }
}
