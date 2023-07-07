using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Global.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using ECS.Unity.Transforms.Components;
using Ipfs;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace Global
{
    public class GlobalWorld : IDisposable
    {
        private readonly CancellationTokenSource destroyCancellationSource = new ();

        private SystemGroupWorld worldSystems;

        public World World { get; private set; }

        public void Initialize(ISceneFactory sceneFactory, Camera unityCamera)
        {
            World = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(World);
            Entity playerEntity = World.Create(new PlayerComponent(), new TransformComponent());

            // not synced by mutex
            var mutex = new MutexSync();

            // Asset Bundle Manifest
            const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

            LoadSceneDefinitionListSystem.InjectToWorld(ref builder, NoCache<SceneDefinitions, GetSceneDefinitionList>.INSTANCE, mutex);
            LoadSceneDefinitionSystem.InjectToWorld(ref builder, NoCache<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>.INSTANCE, mutex);
            LoadSceneSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL, sceneFactory, NoCache<ISceneFacade, GetSceneFacadeIntention>.INSTANCE, mutex);

            CalculateParcelsInRangeSystem.InjectToWorld(ref builder, playerEntity);
            LoadStaticPointersSystem.InjectToWorld(ref builder);
            LoadFixedPointersSystem.InjectToWorld(ref builder);
            LoadPointersByRadiusSystem.InjectToWorld(ref builder);
            ResolveSceneStateByRadiusSystem.InjectToWorld(ref builder);
            ResolveStaticPointersSystem.InjectToWorld(ref builder);
            UnloadSceneSystem.InjectToWorld(ref builder);
            StartSceneSystem.InjectToWorld(ref builder, destroyCancellationSource.Token);

            DebugCameraTransformToPlayerTransformSystem.InjectToWorld(ref builder, playerEntity, unityCamera);

            worldSystems = builder.Finish();
            worldSystems.Initialize();
        }

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();
            World.Dispose();
        }
    }
}
