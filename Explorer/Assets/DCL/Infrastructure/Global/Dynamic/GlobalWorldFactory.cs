using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.RestrictedActions;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.GlobalPartitioning;
using DCL.Ipfs;
using DCL.LOD;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Emotes;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using DCL.Systems;
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
using DCL.RealmNavigation;
using DCL.Roads.Systems;
using ECS.SceneLifeCycle.Systems.EarlyAsset;
using SystemGroups.Visualiser;
using UnityEngine;
using Utility;
using OwnAvatarLoaderFromDebugMenuSystem = DCL.AvatarRendering.AvatarShape.OwnAvatarLoaderFromDebugMenuSystem;

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
        private readonly IDecentralandUrlsSource urlsSource;
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
        private readonly bool useRemoteAssetBundles;
        private readonly HashSet<Vector2Int> roadCoordinates;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly SceneLoadingLimit sceneLoadingLimit;
        private readonly StartParcel startParcel;
        private readonly EntitiesAnalytics entitiesAnalytics;
        private readonly bool isBuilderCollectionPreview;

        public GlobalWorldFactory(in StaticContainer staticContainer,
            CameraSamplingData cameraSamplingData,
            RealmSamplingData realmSamplingData,
            IDecentralandUrlsSource urlsSource,
            IRealmData realmData,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins,
            IDebugContainerBuilder debugContainerBuilder,
            IScenesCache scenesCache,
            HybridSceneParams hybridSceneParams,
            CurrentSceneInfo currentSceneInfo,
            ILODCache lodCache,
            HashSet<Vector2Int> roadCoordinates,
            ILODSettingsAsset lodSettingsAsset,
            IEmotesMessageBus emotesMessageBus,
            World world,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            bool localSceneDevelopment,
            IProfileRepository profileRepository,
            bool useRemoteAssetBundles,
            RoadAssetsPool roadAssetPool,
            SceneLoadingLimit sceneLoadingLimit,
            StartParcel startParcel,
            bool isBuilderCollectionPreview,
            EntitiesAnalytics entitiesAnalytics)
        {
            partitionedWorldsAggregateFactory = staticContainer.SingletonSharedDependencies.AggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            webRequestController = staticContainer.WebRequestsContainer.WebRequestController;
            staticSettings = staticContainer.StaticSettings;
            realmPartitionSettings = staticContainer.RealmPartitionSettings;

            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.urlsSource = urlsSource;
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
            this.useRemoteAssetBundles = useRemoteAssetBundles;
            this.roadAssetPool = roadAssetPool;
            this.sceneLoadingLimit = sceneLoadingLimit;
            this.startParcel = startParcel;
            this.isBuilderCollectionPreview = isBuilderCollectionPreview;
            this.entitiesAnalytics = entitiesAnalytics;

            memoryBudget = staticContainer.SingletonSharedDependencies.MemoryBudget;
        }

        public GlobalWorld Create(ISceneFactory sceneFactory, Entity playerEntity)
        {
            // not synced by mutex, for compatibility only

            ISceneStateProvider globalSceneStateProvider = new SceneStateProvider();
            globalSceneStateProvider.State.Set(SceneState.Running);

            var builder = new ArchSystemsWorldBuilder<World>(world);

            AddShortInfo(world);

            builder
               .InjectCustomGroup(new SyncedPresentationSystemGroup(globalSceneStateProvider))
               .InjectCustomGroup(new SyncedPreRenderingSystemGroup(globalSceneStateProvider));

            IReleasablePerformanceBudget sceneBudget = new ConcurrentLoadingPerformanceBudget(staticSettings.ScenesLoadingBudget);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, webRequestController, localSceneDevelopment, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, entitiesAnalytics);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, webRequestController, localSceneDevelopment, NoCache<SceneEntityDefinition, GetSceneDefinition>.INSTANCE);

            LoadSceneSystemLogicBase loadSceneSystemLogic;

            var assetBundleCdnUrl = URLDomain.FromString(urlsSource.Url(DecentralandUrl.AssetBundlesCDN));

            if (hybridSceneParams.EnableHybridScene)
            {
                string worldContentServerBaseUrl = urlsSource.Url(DecentralandUrl.WorldContentServer);
                URLDomain worldContentServerContentsUrl = URLDomain.FromString(worldContentServerBaseUrl.Replace("/world", "/contents/"));
                loadSceneSystemLogic = new LoadHybridSceneSystemLogic(webRequestController, assetBundleCdnUrl, hybridSceneParams, worldContentServerContentsUrl, worldContentServerBaseUrl);
            }
            else
                loadSceneSystemLogic = new LoadSceneSystemLogic(webRequestController, assetBundleCdnUrl);

            LoadSceneSystem.InjectToWorld(ref builder,
                loadSceneSystemLogic,
                sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE);

            GlobalDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudget, memoryBudget, scenesCache, playerEntity);

            LoadStaticPointersSystem.InjectToWorld(ref builder, roadCoordinates, realmData, urlsSource);
            LoadFixedPointersSystem.InjectToWorld(ref builder, realmData, urlsSource);
            LoadPortableExperiencePointersSystem.InjectToWorld(ref builder, realmData);

            // are replace by increasing radius
            var jobsMathHelper = new ParcelMathJobifiedHelper();
            StartSplittingByRingsSystem.InjectToWorld(ref builder, realmPartitionSettings, jobsMathHelper);

            LoadPointersByIncreasingRadiusSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings,
                partitionSettings, roadCoordinates, realmData, urlsSource);

            //Removed, since we now have landscape surrounding the world
            //CreateEmptyPointersInFixedRealmSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);
            ResolveStaticPointersSystem.InjectToWorld(ref builder, urlsSource);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token, scenesCache, sceneReadinessReportQueue);

            IComponentPool<PartitionComponent> partitionComponentPool = componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>();
            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData, staticContainer.PartitionDataContainer, staticContainer.RealmPartitionSettings);
            PartitionGlobalAssetEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData);

            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings, realmData, cameraSamplingData);
            ResetCameraSamplingDataDirty.InjectToWorld(ref builder, realmData, cameraSamplingData);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            DestroyEntitiesSystem.InjectToWorld(ref builder);

            UpdatePhysicsTickSystem.InjectToWorld(ref builder);
            UpdateTimeSystem.InjectToWorld(ref builder);

            OwnAvatarLoaderFromDebugMenuSystem.InjectToWorld(ref builder, playerEntity, debugContainerBuilder, realmData, profileRepository);

            UnloadPortableExperiencesSystem.InjectToWorld(ref builder);

            UpdateCurrentSceneSystem.InjectToWorld(ref builder, realmData, scenesCache, currentSceneInfo, playerEntity, debugContainerBuilder);

            EarlySceneRequestSystem.InjectToWorld(ref builder, startParcel, realmData, urlsSource);

            LoadSmartWearableSceneSystem.InjectToWorld(ref builder, NoCache<GetSmartWearableSceneIntention.Result, GetSmartWearableSceneIntention>.INSTANCE, webRequestController, sceneFactory, staticContainer.SmartWearableCache);
            LoadSmartWearablePreviewSceneSystem.InjectToWorld(ref builder, webRequestController);

            var pluginArgs = new GlobalPluginArguments(playerEntity, world.Create());

            foreach (IDCLGlobalPlugin plugin in globalPlugins)
                plugin.InjectToWorld(ref builder, pluginArgs);

            var finalizeWorldSystems = new IFinalizeWorldSystem[]
            {
                UnloadSceneSystem.InjectToWorld(ref builder, scenesCache, localSceneDevelopment),
                UnloadSceneLODSystem.InjectToWorld(ref builder, scenesCache, lodCache, staticContainer.RealmPartitionSettings),
                UnloadRoadSystem.InjectToWorld(ref builder, roadAssetPool, scenesCache),
                new ReleaseRealmPooledComponentSystem(componentPoolsRegistry),
                ResolveSceneStateByIncreasingRadiusSystem.InjectToWorld(ref builder, realmPartitionSettings, playerEntity, new VisualSceneStateResolver(lodSettingsAsset), urlsSource, sceneLoadingLimit),
            };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            SystemGroupSnapshot.Instance.Register(GlobalWorld.WORLD_NAME, worldSystems);

            var globalWorld = new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);

            sceneFactory.SetGlobalWorldActions(new GlobalWorldActions(globalWorld.EcsWorld, playerEntity, emotesMessageBus, localSceneDevelopment, useRemoteAssetBundles, isBuilderCollectionPreview));

            return globalWorld;
        }

        private static void AddShortInfo(World world)
        {
            world.Create(new SceneShortInfo(Vector2Int.zero, "global"));
        }
    }
}
