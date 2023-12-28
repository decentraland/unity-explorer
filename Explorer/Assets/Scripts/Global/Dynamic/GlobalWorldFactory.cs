using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character;
using DCL.Character.Components;
using DCL.DebugUtilities;
using DCL.GlobalPartitioning;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.Profiles;
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
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.Unity.Transforms.Components;
using Ipfs;
using SceneRunner;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using SystemGroups.Visualiser;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Avatar = DCL.Profiles.Avatar;

namespace Global.Dynamic
{
    public class GlobalWorldFactory
    {
        private static readonly URLAddress EMPTY_SCENES_MAPPINGS_URL = URLAddress.FromString(
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/EmptyScenes/mappings.json"
#else
            return $"{Application.streamingAssetsPath}/EmptyScenes/mappings.json"
#endif
        );

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
        private readonly IConcurrentBudgetProvider memoryBudgetProvider;
        private readonly StaticSettings staticSettings;

        public GlobalWorldFactory(in StaticContainer staticContainer,
            IRealmPartitionSettings realmPartitionSettings,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData,
            URLDomain assetBundlesURL, IRealmData realmData, IReadOnlyList<IDCLGlobalPlugin> globalPlugins,
            IDebugContainerBuilder debugContainerBuilder)
        {
            partitionedWorldsAggregateFactory = staticContainer.SingletonSharedDependencies.AggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            webRequestController = staticContainer.WebRequestsContainer.WebRequestController;
            staticSettings = staticContainer.StaticSettings;
            this.realmPartitionSettings = realmPartitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.assetBundlesURL = assetBundlesURL;
            this.globalPlugins = globalPlugins;
            this.debugContainerBuilder = debugContainerBuilder;
            this.realmData = realmData;

            memoryBudgetProvider = staticContainer.SingletonSharedDependencies.MemoryBudgetProvider;
            physicsTickProvider = staticContainer.PhysicsTickProvider;
        }

        public GlobalWorld Create(ISceneFactory sceneFactory, IEmptyScenesWorldFactory emptyScenesWorldFactory, ICharacterObject characterObject)
        {
            var world = World.Create();

            // not synced by mutex, for compatibility only
            var mutex = new MutexSync();

            ISceneStateProvider globalSceneStateProvider = new SceneStateProvider();
            globalSceneStateProvider.State = SceneState.Running;

            var builder = new ArchSystemsWorldBuilder<World>(world);
            builder.InjectCustomGroup(new SyncedPostRenderingSystemGroup(mutex, globalSceneStateProvider));

            Entity playerEntity = world.Create(
                new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY),
                new PlayerComponent(characterObject.CameraFocus),
                new TransformComponent { Transform = characterObject.Transform },
                new Profile("fakeOwnUserId", "Player",
                    new Avatar(
                        BodyShape.MALE,
                        WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                        WearablesConstants.DefaultColors.GetRandomEyesColor(),
                        WearablesConstants.DefaultColors.GetRandomHairColor(),
                        WearablesConstants.DefaultColors.GetRandomSkinColor())));

            IConcurrentBudgetProvider sceneBudgetProvider = new ConcurrentLoadingBudgetProvider(staticSettings.ScenesLoadingBudget);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, webRequestController, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, mutex);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, webRequestController, NoCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>.INSTANCE, mutex);

            LoadSceneSystem.InjectToWorld(ref builder,
                new LoadSceneSystemLogic(webRequestController, assetBundlesURL),
                new LoadEmptySceneSystemLogic(webRequestController, emptyScenesWorldFactory, componentPoolsRegistry, EMPTY_SCENES_MAPPINGS_URL),
                sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE, mutex);

            GlobalDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudgetProvider, memoryBudgetProvider);

            CalculateParcelsInRangeSystem.InjectToWorld(ref builder, playerEntity);
            LoadStaticPointersSystem.InjectToWorld(ref builder);
            LoadFixedPointersSystem.InjectToWorld(ref builder);

            // Archaic systems
            //LoadPointersByRadiusSystem.InjectToWorld(ref builder);
            //ResolveSceneStateByRadiusSystem.InjectToWorld(ref builder);

            // are replace by increasing radius
            var jobsMathHelper = new ParcelMathJobifiedHelper();
            StartSplittingByRingsSystem.InjectToWorld(ref builder, realmPartitionSettings, jobsMathHelper);
            LoadPointersByIncreasingRadiusSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);
            ResolveSceneStateByIncreasingRadiusSystem.InjectToWorld(ref builder, realmPartitionSettings);
            CreateEmptyPointersInFixedRealmSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);

            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            UnloadSceneSystem.InjectToWorld(ref builder);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token);

            IComponentPool<PartitionComponent> partitionComponentPool = componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>();
            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData);
            PartitionGlobalAssetEntitiesSystem.InjectToWorld(ref builder, partitionComponentPool, partitionSettings, cameraSamplingData);

            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings, realmData);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            DestroyEntitiesSystem.InjectToWorld(ref builder);

            UpdatePhysicsTickSystem.InjectToWorld(ref builder, physicsTickProvider);
            UpdateTimeSystem.InjectToWorld(ref builder);

            OwnAvatarLoaderFromDebugMenuSystem.InjectToWorld(ref builder, playerEntity, debugContainerBuilder, realmData);

            var pluginArgs = new GlobalPluginArguments(playerEntity);

            foreach (IDCLGlobalPlugin plugin in globalPlugins)
                plugin.InjectToWorld(ref builder, pluginArgs);

            var finalizeWorldSystems = new IFinalizeWorldSystem[] { new ReleaseRealmPooledComponentSystem(componentPoolsRegistry) };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            SystemGroupSnapshot.Instance.Register(GlobalWorld.WORLD_NAME, worldSystems);

            return new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);
        }
    }
}
