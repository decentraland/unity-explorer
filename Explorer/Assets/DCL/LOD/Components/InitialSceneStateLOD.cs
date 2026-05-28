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
        private readonly List<ISSStoredAsset> Assets = new ();
        public string SceneID { get; private set; } = string.Empty;
        public GameObject ParentContainer { get; private set; }
        public IGltfContainerAssetsCache gltfCache { get; private set; }
        public int TotalAssetsToInstantiate { get; private set; }
        public AssetBundleData? AssetBundleData { get; private set; }

        public bool AssetsShouldGoToTheBridge;

        public enum State
        {
            UNINITIALIZED,
            PROCESSING,
            FAILED,
            RESOLVED
        }

        public State CurrentState;

        public AssetPromise<AssetBundleData, GetAssetBundleIntention> AssetBundlePromise;

        public int Generation { get; private set; }

        public void ForgetLoading(World world)
        {
            if (CurrentState is State.FAILED or State.RESOLVED)
                return;

            AssetBundlePromise.ForgetLoading(world);

            if (CurrentState is State.PROCESSING)
            {
                Generation++;
                Clear();
            }

            CurrentState = State.UNINITIALIZED;
        }

        private void Clear()
        {
            AssetBundleData?.Dereference();
            AssetBundleData = null;

            foreach (ISSStoredAsset gltfContainerAsset in Assets)
            {
                if (gltfContainerAsset.succeded)
                    gltfCache.Dereference(gltfContainerAsset.AssetHash, gltfContainerAsset.Asset, AssetsShouldGoToTheBridge);
            }

            Assets.Clear();
        }

        public void Dispose(World world)
        {
            AssetBundlePromise.ForgetLoading(world);
            Clear();
            UnityObjectUtils.SafeDestroy(ParentContainer);
        }

        public void AddResolvedAsset(string assetHash, GltfContainerAsset asset) =>
            Assets.Add(new ISSStoredAsset
            {
                AssetHash = assetHash,
                Asset = asset,
                succeded = true,
            });

        public bool AllAssetsInstantiated() =>
            ParentContainer != null && Assets.Count == TotalAssetsToInstantiate;

        public bool IsProcessing() =>
            CurrentState is State.PROCESSING;


        public void Initialize(string sceneID, Vector3 sceneGeometryBaseParcelPosition, AssetBundleData resultAsset, IGltfContainerAssetsCache gltfContainerAssetsCache, int assetHashCount)
        {
            EnsureParentContainer(sceneID, sceneGeometryBaseParcelPosition);
            AssetBundleData = resultAsset;
            gltfCache = gltfContainerAssetsCache;
            TotalAssetsToInstantiate = assetHashCount;
        }

        /// <summary>
        ///     Descriptor-only initialization: no shared ISS bundle to hold; each asset will arrive via its own promise.
        /// </summary>
        public void InitializeFromDescriptor(string sceneID, Vector3 sceneGeometryBaseParcelPosition, IGltfContainerAssetsCache gltfContainerAssetsCache, int assetHashCount)
        {
            EnsureParentContainer(sceneID, sceneGeometryBaseParcelPosition);
            AssetBundleData = null;
            gltfCache = gltfContainerAssetsCache;
            TotalAssetsToInstantiate = assetHashCount;
        }

        private void EnsureParentContainer(string sceneID, Vector3 sceneGeometryBaseParcelPosition)
        {
            SceneID = sceneID;
            if (ParentContainer == null)
                ParentContainer = new GameObject($"{sceneID}_ISS_LOD");
            ParentContainer.transform.position = sceneGeometryBaseParcelPosition;
        }

        public void AddFailedAsset(string creationHelperAssetHash)
        {
            Assets.Add(new ISSStoredAsset
            {
                AssetHash = creationHelperAssetHash,
                succeded = false,
            });
        }

        private struct ISSStoredAsset
        {
            public string AssetHash;
            public GltfContainerAsset Asset;
            public bool succeded;
        }
    }
}
