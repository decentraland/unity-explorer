using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.RestrictedActions;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.GlobalPartitioning;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Multiplayer.Emotes;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.Systems;
using DCL.Time;
using DCL.Time.Systems;
using DCL.WebRequests;
using ECS;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Prioritization.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.CurrentScene;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using SceneRunner;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using DCL.Profiles;
using DCL.Roads.Systems;
using SystemGroups.Visualiser;
using UnityEngine;
using Utility;

namespace Global.Dynamic
{
    public class GlobalWorldFactory
    {
        private readonly CameraSamplingData cameraSamplingData;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly CancellationTokenSource destroyCancellationSource = new ();
        private readonly ISystemGroupAggregate<IPartitionComponent>.IFactory partitionedWorldsAggregateFactory;
        private readonly IPartitionSettings partitionSettings;
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly RealmSamplingData realmSamplingData;
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundlesURL;
        private readonly PhysicsTickProvider physicsTickProvider;
        private readonly IWebRequestController webRequestController;
        private readonly IReadOnlyList<IDCLGlobalPlugin> globalPlugins;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IPerformanceBudget memoryBudget;
        private readonly StaticSettings staticSettings;
        private readonly StaticContainer staticContainer;
        private readonly IScenesCache scenesCache;
        private readonly ILODCache lodCache;
        private readonly IRoadAssetPool roadAssetPool;
        private readonly IEmotesMessageBus emotesMessageBus;
        private readonly World world;
        private readonly CurrentSceneInfo currentSceneInfo;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly HybridSceneParams hybridSceneParams;
        private readonly bool localSceneDevelopment;
        private readonly IProfileRepository profileRepository;
        private readonly HashSet<Vector2Int> roadCoordinates;
        private readonly ILODSettingsAsset lodSettingsAsset;

        public GlobalWorldFactory(in StaticContainer staticContainer,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData,
            URLDomain assetBundlesURL, IRealmData realmData,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins, IDebugContainerBuilder debugContainerBuilder,
            IScenesCache scenesCache, HybridSceneParams hybridSceneParams,
            CurrentSceneInfo currentSceneInfo,
            ILODCache lodCache,
            HashSet<Vector2Int> roadCoordinates,
            ILODSettingsAsset lodSettingsAsset,
            IEmotesMessageBus emotesMessageBus,
            World world,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            bool localSceneDevelopment,
            IProfileRepository profileRepository,
            RoadAssetsPool roadAssetPool)
        {
            partitionedWorldsAggregateFactory = staticContainer.SingletonSharedDependencies.AggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            webRequestController = staticContainer.WebRequestsContainer.WebRequestController;
            staticSettings = staticContainer.StaticSettings;
            realmPartitionSettings = staticContainer.RealmPartitionSettings;

            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.assetBundlesURL = assetBundlesURL;
            this.globalPlugins = globalPlugins;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realmData = realmData;
            this.staticContainer = staticContainer;
            this.scenesCache = scenesCache;
            this.hybridSceneParams = hybridSceneParams;
            this.currentSceneInfo = currentSceneInfo;
            this.lodCache = lodCache;
            this.emotesMessageBus = emotesMessageBus;
            this.localSceneDevelopment = localSceneDevelopment;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.world = world;
            this.profileRepository = profileRepository;
            this.roadCoordinates = roadCoordinates;
            this.lodSettingsAsset = lodSettingsAsset;
            this.roadAssetPool = roadAssetPool;

            memoryBudget = staticContainer.SingletonSharedDependencies.MemoryBudget;
            physicsTickProvider = staticContainer.PhysicsTickProvider;
        }

        public GlobalWorld Create(ISceneFactory sceneFactory, Entity playerEntity)
        {
            // not synced by mutex, for compatibility only

            ISceneStateProvider globalSceneStateProvider = new SceneStateProvider();
            globalSceneStateProvider.State = SceneState.Running;

            var builder = new ArchSystemsWorldBuilder<World>(world);

            AddShortInfo(world);

            builder
               .InjectCustomGroup(new SyncedPresentationSystemGroup(globalSceneStateProvider))
               .InjectCustomGroup(new SyncedPreRenderingSystemGroup(globalSceneStateProvider));

            IReleasablePerformanceBudget sceneBudget = new ConcurrentLoadingPerformanceBudget(staticSettings.ScenesLoadingBudget);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, webRequestController, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, webRequestController, NoCache<SceneEntityDefinition, GetSceneDefinition>.INSTANCE);

            LoadSceneSystemLogicBase loadSceneSystemLogic;

            if (hybridSceneParams.EnableHybridScene)
                loadSceneSystemLogic = new LoadHybridSceneSystemLogic(webRequestController, assetBundlesURL, hybridSceneParams);
            else
                loadSceneSystemLogic = new LoadSceneSystemLogic(webRequestController, assetBundlesURL);

            LoadSceneSystem.InjectToWorld(ref builder,
                loadSceneSystemLogic,
                sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE);

            GlobalDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudget, memoryBudget, scenesCache, playerEntity);

            LoadStaticPointersSystem.InjectToWorld(ref builder, roadCoordinates, realmData);
            LoadFixedPointersSystem.InjectToWorld(ref builder, realmData);
            LoadPortableExperiencePointersSystem.InjectToWorld(ref builder, realmData);

            // are replace by increasing radius
            var jobsMathHelper = new ParcelMathJobifiedHelper();
            StartSplittingByRingsSystem.InjectToWorld(ref builder, realmPartitionSettings, jobsMathHelper);

            LoadPointersByIncreasingRadiusSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings,
                partitionSettings, sceneReadinessReportQueue, scenesCache, roadCoordinates, realmData);


            //Removed, since we now have landscape surrounding the world
            //CreateEmptyPointersInFixedRealmSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);

            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token, scenesCache, sceneReadinessReportQueue);

            IComponentPool<PartitionComponent> partitionComponentPool = componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>();
            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData, staticContainer.PartitionDataContainer, staticContainer.RealmPartitionSettings);
            PartitionGlobalAssetEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData);

            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings, realmData, cameraSamplingData);
            ResetCameraSamplingDataDirty.InjectToWorld(ref builder, realmData, cameraSamplingData);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            DestroyEntitiesSystem.InjectToWorld(ref builder);

            UpdatePhysicsTickSystem.InjectToWorld(ref builder, physicsTickProvider);
            UpdateTimeSystem.InjectToWorld(ref builder);

            OwnAvatarLoaderFromDebugMenuSystem.InjectToWorld(ref builder, playerEntity, debugContainerBuilder, realmData, profileRepository);

            UnloadPortableExperiencesSystem.InjectToWorld(ref builder);

            UpdateCurrentSceneSystem.InjectToWorld(ref builder, realmData, scenesCache, currentSceneInfo, playerEntity, debugContainerBuilder);

            var pluginArgs = new GlobalPluginArguments(playerEntity);

            foreach (IDCLGlobalPlugin plugin in globalPlugins)
                plugin.InjectToWorld(ref builder, pluginArgs);

            var sceneLoadingLimit
                = SceneLoadingLimit.CreateMax();

            var finalizeWorldSystems = new IFinalizeWorldSystem[]
            {
                UnloadSceneSystem.InjectToWorld(ref builder, scenesCache, localSceneDevelopment),
                UnloadSceneLODSystem.InjectToWorld(ref builder, scenesCache, lodCache),
                UnloadRoadSystem.InjectToWorld(ref builder, roadAssetPool, scenesCache),
                new ReleaseRealmPooledComponentSystem(componentPoolsRegistry),
                ResolveSceneStateByIncreasingRadiusSystem.InjectToWorld(ref builder, realmPartitionSettings, playerEntity, new VisualSceneStateResolver(lodSettingsAsset), realmData, sceneLoadingLimit),
            };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            SystemGroupSnapshot.Instance.Register(GlobalWorld.WORLD_NAME, worldSystems);

            var globalWorld = new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);

            sceneFactory.SetGlobalWorldActions(new GlobalWorldActions(globalWorld.EcsWorld, playerEntity, emotesMessageBus));

            return globalWorld;
        }

        private static void AddShortInfo(World world)
        {
            world.Create(new SceneShortInfo(Vector2Int.zero, "global"));
        }
    }
}
