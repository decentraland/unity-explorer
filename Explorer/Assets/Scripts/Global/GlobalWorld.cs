using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Global.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Transforms.Components;
using Ipfs;
using JetBrains.Annotations;
using SceneRunner;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace Global
{
    public class GlobalWorld : IDisposable
    {
        private SceneLifeCycleState state;

        private IIpfsRealm ipfsRealm;

        private SystemGroupWorld worldSystems;

        private World world;

        private readonly CancellationTokenSource destroyCancellationSource = new ();

        public void Initialize(ISceneFactory sceneFactory, Camera unityCamera, int sceneLoadRadius, [CanBeNull] List<Vector2Int> staticLoadPositions = null)
        {
            ipfsRealm = new IpfsRealm("https://sdk-test-scenes.decentraland.zone/");
            world = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            state = new SceneLifeCycleState()
            {
                PlayerEntity = world.Create(new PlayerComponent(), new TransformComponent()),
                SceneLoadRadius = sceneLoadRadius,
            };

            LoadScenesDynamicallySystem.InjectToWorld(ref builder, ipfsRealm, state, staticLoadPositions);
            ResolveScenesStateSystem.InjectToWorld(ref builder, state);
            StartSceneSystem.InjectToWorld(ref builder, ipfsRealm, sceneFactory, destroyCancellationSource.Token);
            DestroySceneSystem.InjectToWorld(ref builder);

            // Asset Bundle Manifest
            const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

            var assetBundlesManifestCache = new AssetBundlesManifestCache();
            var mutex = new MutexSync();
            PrepareAssetBundleManifestParametersSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL);

            //TODO: Should we create a concurrent loading provider only for scenes?
            ConcurrentLoadingBudgetProvider sceneBudgetProvider = new ConcurrentLoadingBudgetProvider(100);
            LoadAssetBundleManifestSystem.InjectToWorld(ref builder, assetBundlesManifestCache, ASSET_BUNDLES_URL, mutex, sceneBudgetProvider);
            AssetBundleDeferredLoadingSystem.InjectToWorld(ref builder, sceneBudgetProvider);

            DebugCameraTransformToPlayerTransformSystem.InjectToWorld(ref builder, state.PlayerEntity, unityCamera);

            worldSystems = builder.Finish();
            worldSystems.Initialize();
        }

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();
            world.Dispose();
        }
    }
}
