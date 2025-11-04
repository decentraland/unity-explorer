using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public class InitialSceneStateLOD
    {
        private List<(string, GltfContainerAsset)> Assets = new ();
        public GameObject ParentContainer { get; private set; }
        public IGltfContainerAssetsCache gltfCache { get; private set; }
        public int TotalAssetsToInstantiate { get; private set; }
        public AssetBundleData? AssetBundleData { get; private set; }

        public bool AssetsShouldGoToTheBridge;

        public enum InitialSceneStateLODState
        {
            UNINITIALIZED,
            PROCESSING,
            FAILED,
            RESOLVED
        }

        public InitialSceneStateLODState CurrentState;

        public AssetPromise<AssetBundleData, GetAssetBundleIntention> AssetBundlePromise;

        public void ForgetLoading(World world)
        {
            if (CurrentState is InitialSceneStateLODState.FAILED or InitialSceneStateLODState.RESOLVED)
                return;

            AssetBundlePromise.ForgetLoading(world);

            if (CurrentState is InitialSceneStateLODState.PROCESSING)
                Clear();

            CurrentState = InitialSceneStateLODState.UNINITIALIZED;
        }

        private void Clear()
        {
            AssetBundleData?.Dereference();
            AssetBundleData = null;

            foreach ((string, GltfContainerAsset) gltfContainerAsset in Assets)
                gltfCache.Dereference(gltfContainerAsset.Item1, gltfContainerAsset.Item2, AssetsShouldGoToTheBridge);

            Assets.Clear();
            UnityObjectUtils.SafeDestroy(ParentContainer);
        }

        public void Dispose(World world)
        {
            AssetBundlePromise.ForgetLoading(world);
            Clear();
        }

        public void AddResolvedAsset(string assetHash, GltfContainerAsset asset) =>
            Assets.Add((assetHash, asset));

        public bool AllAssetsInstantiated() =>
            AssetBundleData != null && Assets.Count == TotalAssetsToInstantiate;

        public bool IsProcessing() =>
            CurrentState is InitialSceneStateLODState.PROCESSING;


        public void Initialize(string sceneID, Vector3 sceneGeometryBaseParcelPosition, AssetBundleData resultAsset, IGltfContainerAssetsCache gltfContainerAssetsCache, int assetHashCount)
        {
            ParentContainer = new GameObject($"{sceneID}_ISS_LOD");
            ParentContainer.transform.position = sceneGeometryBaseParcelPosition;
            AssetBundleData = resultAsset;
            gltfCache = gltfContainerAssetsCache;
            TotalAssetsToInstantiate = assetHashCount;
        }
    }
}
