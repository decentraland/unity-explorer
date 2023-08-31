using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Character;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.PluginSystem.Global;
using ECS.ComponentsPooling;
using ECS.LifeCycle;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Prioritization.Systems;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.DeferredLoading;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using Ipfs;
using SceneRunner;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace Global.Dynamic
{
    public class GlobalWorldFactory
    {
        private static readonly string EMPTY_SCENES_MAPPINGS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/EmptyScenes/mappings.json";
#else
            return $"{Application.streamingAssetsPath}/EmptyScenes/mappings.json";
#endif

        private readonly CameraSamplingData cameraSamplingData;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly CancellationTokenSource destroyCancellationSource = new ();
        private readonly ISystemGroupAggregate<IPartitionComponent>.IFactory partitionedWorldsAggregateFactory;
        private readonly IPartitionSettings partitionSettings;
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly RealmSamplingData realmSamplingData;
        private readonly IReadOnlyList<IDCLGlobalPlugin> globalPlugins;

        public GlobalWorldFactory(in StaticContainer staticContainer, IRealmPartitionSettings realmPartitionSettings,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData, IReadOnlyList<IDCLGlobalPlugin> globalPlugins)
        {
            partitionedWorldsAggregateFactory = staticContainer.SingletonSharedDependencies.AggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            this.realmPartitionSettings = realmPartitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.globalPlugins = globalPlugins;
        }

        public GlobalWorld Create(ISceneFactory sceneFactory, IEmptyScenesWorldFactory emptyScenesWorldFactory, ICharacterObject characterObject)
        {
            var world = World.Create();

            // not synced by mutex, for compatibility only
            var mutex = new MutexSync();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            Entity playerEntity = world.Create(
                new CRDTEntity(SpecialEntititiesID.PLAYER_ENTITY),
                new PlayerComponent(characterObject.CameraFocus),
                new TransformComponent { Transform = characterObject.Transform }
            );

            world.Create(new PBAvatarShape
            {
                BodyShape = "urn:decentraland:off-chain:base-avatars:BaseMale",
                Wearables =
                {
                    //TODO: Fix broken material
                    //"urn:decentraland:off-chain:base-avatars:black_glove",
                    //TODO: Fix no asset bundle manifest
                    //"urn:decentraland:off-chain:base-avatars:square_earring"
                    "urn:decentraland:off-chain:base-avatars:green_hoodie",
                    "urn:decentraland:off-chain:base-avatars:cool_hair",
                    "urn:decentraland:off-chain:base-avatars:brown_pants",
                    "urn:decentraland:off-chain:base-avatars:bun_shoes",
                },
            });

            world.Create(new PBAvatarShape
            {
                BodyShape = "urn:decentraland:off-chain:base-avatars:BaseMale",
                Wearables =
                {
                    //TODO: Fix broken material
                    //"urn:decentraland:off-chain:base-avatars:black_glove",
                    //TODO: Fix no asset bundle manifest
                    //"urn:decentraland:off-chain:base-avatars:square_earring"
                    "urn:decentraland:off-chain:base-avatars:light_green_shirt",
                    "urn:decentraland:off-chain:base-avatars:keanu_hair",
                    "urn:decentraland:off-chain:base-avatars:jean_shorts",
                    "urn:decentraland:off-chain:base-avatars:sport_colored_shoes",
                },
            });

            //TODO: Avoid initializing dictionary here
            world.Create(new WearableCatalog
            {
                catalog = new Dictionary<string, EntityReference>(),
            });

            // Asset Bundle Manifest
            const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

            IConcurrentBudgetProvider sceneBudgetProvider = new ConcurrentLoadingBudgetProvider(100);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, mutex);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, NoCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>.INSTANCE, mutex);

            LoadSceneSystem.InjectToWorld(ref builder,
                new LoadSceneSystemLogic(ASSET_BUNDLES_URL),
                new LoadEmptySceneSystemLogic(emptyScenesWorldFactory, componentPoolsRegistry, EMPTY_SCENES_MAPPINGS_URL),
                sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE, mutex);

            SceneLifeCycleDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudgetProvider);

            CalculateParcelsInRangeSystem.InjectToWorld(ref builder, playerEntity);
            LoadStaticPointersSystem.InjectToWorld(ref builder);
            LoadFixedPointersSystem.InjectToWorld(ref builder);

            // Archaic systems
            // LoadPointersByRadiusSystem.InjectToWorld(ref builder);
            // ResolveSceneStateByRadiusSystem.InjectToWorld(ref builder);
            // are replace by increasing radius
            var jobsMathHelper = new ParcelMathJobifiedHelper();
            StartSplittingByRingsSystem.InjectToWorld(ref builder, realmPartitionSettings, jobsMathHelper);
            LoadPointersByIncreasingRadiusSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);
            ResolveSceneStateByIncreasingRadiusSystem.InjectToWorld(ref builder, realmPartitionSettings);
            CreateEmptyPointersInFixedRealmSystem.InjectToWorld(ref builder, jobsMathHelper, realmPartitionSettings);

            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            UnloadSceneSystem.InjectToWorld(ref builder);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token);

            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>(), partitionSettings, cameraSamplingData);
            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            var pluginArgs = new GlobalPluginArguments(playerEntity);

            for (var i = 0; i < globalPlugins.Count; i++)
                globalPlugins[i].InjectToWorld(ref builder, pluginArgs);

            var finalizeWorldSystems = new IFinalizeWorldSystem[]
                { new ReleaseRealmPooledComponentSystem(componentPoolsRegistry) };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            return new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);
        }
    }
}
