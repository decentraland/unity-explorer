using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Global.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles.Manifest;
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
        private readonly CancellationTokenSource destroyCancellationSource = new ();

        private ProcessRealmChangeSystem processRealmChangeSystem;
        private SceneLifeCycleState state;

        private World world;

        private SystemGroupWorld worldSystems;

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();
            world.Dispose();
        }

        public void Initialize(ISceneFactory sceneFactory, Camera unityCamera, int sceneLoadRadius, [CanBeNull] List<Vector2Int> staticLoadPositions = null)
        {
            world = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            state = new SceneLifeCycleState
            {
                PlayerEntity = world.Create(new PlayerComponent(), new TransformComponent()),
                SceneLoadRadius = sceneLoadRadius,
            };

            processRealmChangeSystem = ProcessRealmChangeSystem.InjectToWorld(ref builder, state);
            LoadScenesDynamicallySystem.InjectToWorld(ref builder, state, staticLoadPositions);
            ResolveScenesStateSystem.InjectToWorld(ref builder, state);
            LoadSceneMetadataSystem.InjectToWorld(ref builder, state);
            LoadSceneSystem.InjectToWorld(ref builder, state);
            StartSceneSystem.InjectToWorld(ref builder, state, sceneFactory, destroyCancellationSource.Token);
            DestroySceneSystem.InjectToWorld(ref builder);

            // Asset Bundle Manifest
            const string ASSET_BUNDLES_URL = "https://ab-cdn.decentraland.org/";

            var assetBundlesManifestCache = new AssetBundlesManifestCache();
            var mutex = new MutexSync();
            PrepareAssetBundleManifestParametersSystem.InjectToWorld(ref builder, ASSET_BUNDLES_URL);
            LoadAssetBundleManifestSystem.InjectToWorld(ref builder, assetBundlesManifestCache, ASSET_BUNDLES_URL, mutex);

            DebugCameraTransformToPlayerTransformSystem.InjectToWorld(ref builder, state.PlayerEntity, unityCamera);

            worldSystems = builder.Finish();
            worldSystems.Initialize();
        }

        public void SetRealm(string realm)
        {
            processRealmChangeSystem.ChangeRealm(realm);
        }
    }
}
