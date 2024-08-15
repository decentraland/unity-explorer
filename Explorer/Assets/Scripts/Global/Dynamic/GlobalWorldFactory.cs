using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.RestrictedActions;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Plugin;
using DCL.DebugUtilities;
using DCL.GlobalPartitioning;
using DCL.Ipfs;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
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
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using SceneRunner;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using DCL.LOD;
using SystemGroups.Visualiser;
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
        private readonly CharacterContainer characterContainer;

        private readonly HybridSceneParams hybridSceneParams;
        private readonly ICharacterDataPropagationUtility characterDataPropagationUtility;

        public GlobalWorldFactory( in StaticContainer staticContainer,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData,
            URLDomain assetBundlesURL, IRealmData realmData,
            IReadOnlyList<IDCLGlobalPlugin> globalPlugins, IDebugContainerBuilder debugContainerBuilder,
            IScenesCache scenesCache, HybridSceneParams hybridSceneParams,
            ICharacterDataPropagationUtility characterDataPropagationUtility,
            ILODCache lodCache)
        {
            partitionedWorldsAggregateFactory = staticContainer.SingletonSharedDependencies.AggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            webRequestController = staticContainer.WebRequestsContainer.WebRequestController;
            staticSettings = staticContainer.StaticSettings;
            characterContainer = staticContainer.CharacterContainer;
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
            this.characterDataPropagationUtility = characterDataPropagationUtility;
            this.lodCache = lodCache;

            memoryBudget = staticContainer.SingletonSharedDependencies.MemoryBudget;
            physicsTickProvider = staticContainer.PhysicsTickProvider;
        }

        public (GlobalWorld, Entity) Create(ISceneFactory sceneFactory)
        {
            var world = World.Create();

            // not synced by mutex, for compatibility only

            ISceneStateProvider globalSceneStateProvider = new SceneStateProvider();
            globalSceneStateProvider.State = SceneState.Running;

            var builder = new ArchSystemsWorldBuilder<World>(world);
            builder.InjectCustomGroup(new SyncedPreRenderingSystemGroup(null, globalSceneStateProvider));

            Entity playerEntity = characterContainer.CreatePlayerEntity(world);

            IReleasablePerformanceBudget sceneBudget = new ConcurrentLoadingPerformanceBudget(staticSettings.ScenesLoadingBudget);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, webRequestController, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, webRequestController, NoCache<SceneEntityDefinition, GetSceneDefinition>.INSTANCE);

            LoadSceneSystemLogicBase loadSceneSystemLogic;

            if (hybridSceneParams.EnableHybridScene)
                loadSceneSystemLogic = new LoadHybridSceneSystemLogic(webRequestController, assetBundlesURL, hybridSceneParams, characterDataPropagationUtility, world, playerEntity);
            else
                loadSceneSystemLogic = new LoadSceneSystemLogic(webRequestController, assetBundlesURL, characterDataPropagationUtility, world, playerEntity);


            LoadSceneSystem.InjectToWorld(ref builder,
                loadSceneSystemLogic,
                new LoadEmptySceneSystemLogic(),
                sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE);

            GlobalDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudget, memoryBudget, staticContainer.SingletonSharedDependencies.SceneAssetLock);

            LoadStaticPointersSystem.InjectToWorld(ref builder);
            LoadFixedPointersSystem.InjectToWorld(ref builder);
            LoadPortableExperiencePointersSystem.InjectToWorld(ref builder);

            // are replace by increasing radius
            var jobsMathHelper = new ParcelMathJobifiedHelper();
            StartSplittingByRingsSystem.InjectToWorld(ref builder, realmPartitionSettings, jobsMathHelper);

            LoadPointersByIncreasingRadiusSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings,
                partitionSettings);

            ResolveSceneStateByIncreasingRadiusSystem.InjectToWorld(ref builder, realmPartitionSettings);
            //Removed, since we now have landscape surrounding the world
            //CreateEmptyPointersInFixedRealmSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);

            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token, scenesCache);

            IComponentPool<PartitionComponent> partitionComponentPool = componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>();
            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData, staticContainer.PartitionDataContainer, staticContainer.RealmPartitionSettings);
            PartitionGlobalAssetEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData);

            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings, realmData, cameraSamplingData);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            DestroyEntitiesSystem.InjectToWorld(ref builder);

            UpdatePhysicsTickSystem.InjectToWorld(ref builder, physicsTickProvider);
            UpdateTimeSystem.InjectToWorld(ref builder);

            OwnAvatarLoaderFromDebugMenuSystem.InjectToWorld(ref builder, playerEntity, debugContainerBuilder, realmData);

            UnloadPortableExperiencesSystem.InjectToWorld(ref builder);

            UpdateCurrentSceneSystem.InjectToWorld(ref builder, realmData, scenesCache, playerEntity, staticContainer.SingletonSharedDependencies.SceneAssetLock);

            IEmoteProvider emoteProvider = new EcsEmoteProvider(world, realmData);

            var pluginArgs = new GlobalPluginArguments(playerEntity, emoteProvider);

            foreach (IDCLGlobalPlugin plugin in globalPlugins)
                plugin.InjectToWorld(ref builder, pluginArgs);

            var finalizeWorldSystems = new IFinalizeWorldSystem[]
            {
                UnloadSceneSystem.InjectToWorld(ref builder, scenesCache, staticContainer.SingletonSharedDependencies.SceneAssetLock), UnloadSceneLODSystem.InjectToWorld(ref builder, scenesCache, lodCache),
                new ReleaseRealmPooledComponentSystem(componentPoolsRegistry),
            };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            SystemGroupSnapshot.Instance.Register(GlobalWorld.WORLD_NAME, worldSystems);

            var globalWorld = new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);

            staticContainer.GlobalWorldProxy.SetObject(world);

            sceneFactory.SetGlobalWorldActions(new GlobalWorldActions(globalWorld.EcsWorld, playerEntity));

            return (globalWorld, playerEntity);
        }
    }
}
