using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.BudgetProvider;
using ECS.ComponentsPooling;
using ECS.LifeCycle;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Prioritization.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.DeferredLoading;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.Unity.Transforms.Components;
using Ipfs;
using SceneRunner;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

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
        private readonly IConcurrentBudgetProvider instantiationBudgetProvider;
        private readonly IConcurrentBudgetProvider sceneBudgetProvider;

        public GlobalWorldFactory(in StaticContainer staticContainer, IRealmPartitionSettings realmPartitionSettings,
            CameraSamplingData cameraSamplingData, RealmSamplingData realmSamplingData)
        {
            partitionedWorldsAggregateFactory = staticContainer.WorldsAggregateFactory;
            componentPoolsRegistry = staticContainer.ComponentsContainer.ComponentPoolsRegistry;
            partitionSettings = staticContainer.PartitionSettings;
            this.realmPartitionSettings = realmPartitionSettings;
            this.cameraSamplingData = cameraSamplingData;
            this.realmSamplingData = realmSamplingData;
            this.instantiationBudgetProvider = staticContainer.InstantiationBudgetProvider;
            this.sceneBudgetProvider = new ConcurrentBudgetProvider(100);
        }

        public GlobalWorld Create(ISceneFactory sceneFactory, Camera unityCamera)
        {
            var world = World.Create();

            // not synced by mutex, for compatibility only
            var mutex = new MutexSync();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            Entity playerEntity = world.Create(new PlayerComponent(), new TransformComponent { Transform = unityCamera.transform }, new CameraComponent(unityCamera), cameraSamplingData);

            // Asset Bundle Manifest
            const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

            ResetFrameTimeBudgetProviderSystem.InjectToWorld(ref builder, instantiationBudgetProvider);

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, mutex);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, NoCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>.INSTANCE, mutex);

            LoadSceneSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL, sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE, mutex);

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

            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            UnloadSceneSystem.InjectToWorld(ref builder);
            ControlSceneUpdateLoopSystem.InjectToWorld(ref builder, realmPartitionSettings, destroyCancellationSource.Token);

            PartitionSceneEntitiesSystem.InjectToWorld(ref builder, componentPoolsRegistry.GetReferenceTypePool<PartitionComponent>(), partitionSettings, cameraSamplingData);
            CheckCameraQualifiedForRepartitioningSystem.InjectToWorld(ref builder, partitionSettings);
            SortWorldsAggregateSystem.InjectToWorld(ref builder, partitionedWorldsAggregateFactory, realmPartitionSettings);

            var finalizeWorldSystems = new IFinalizeWorldSystem[]
                { new ReleaseRealmPooledComponentSystem(componentPoolsRegistry) };

            SystemGroupWorld worldSystems = builder.Finish();
            worldSystems.Initialize();

            return new GlobalWorld(world, worldSystems, finalizeWorldSystems, cameraSamplingData, realmSamplingData, destroyCancellationSource);
        }
    }
}
