using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Character;
using DCL.Character.Components;
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

            world.Create(new WearableComponent
                { urn = "urn:decentraland:off-chain:base-avatars:BaseMale" });

            world.Create(new WearableComponent
                { urn = "urn:decentraland:off-chain:base-avatars:black_glove" });

            world.Create(new WearableComponent
                { urn = "urn:decentraland:off-chain:base-avatars:m_sweater_02" });

            world.Create(new WearableComponent
                { urn = "urn:decentraland:off-chain:base-avatars:cool_hair" });

            //world.Create(new WearableComponent() { urn = "urn:decentraland:off-chain:base-avatars:citycomfortableshoes" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:matic:collections-v2:0x7b207598a9167e8d993251e990aea23c29203ca3:0" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:matic:collections-v2:0x26ea2f6a7273a2f28b410406d1c13ff7d4c9a162:5" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:matic:collections-v2:0x167d6b63511a7b5062d1f7b07722fccbbffb5105:2" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:matic:collections-v2:0x94f128b1f2bd7fdc786b005652569267cd9268fa:1" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:off-chain:base-avatars:Espadrilles" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:off-chain:base-avatars:beard" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:off-chain:base-avatars:eyebrows_00" });
            //world.Create(new WearableComponent() { urn = "urn:decentraland:off-chain:base-avatars:eyes_00" });

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
