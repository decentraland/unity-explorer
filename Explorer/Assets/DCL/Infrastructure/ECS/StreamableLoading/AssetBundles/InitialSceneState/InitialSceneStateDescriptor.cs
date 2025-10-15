    using Arch.Core;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    public class InitialSceneStateDescriptor
    {
        public StreamableLoadingResult<AssetBundleData> AssetBundleData;
        public AssetBundlePromise AssetBundlePromise;
        public List<(string, GltfContainerAsset)> AssetsInstantiated;

        private World GlobalWorld;
        private string SceneID;
        private IGltfContainerAssetsCache assetsCache;

        private bool AllAssetsInstantiated;

        public bool IsReady()
        {
            if (!IsSupported())
                return true;

            if (AssetBundleData.IsInitialized && AllAssetsInstantiated)
                return true;

            if (!AssetBundleData.IsInitialized && AssetBundlePromise == AssetBundlePromise.NULL)
            {
                //TOO (JUANI): Here we will use the sceneID to create the promise
                AssetBundlePromise = AssetBundlePromise.Create(GlobalWorld,
                    GetAssetBundleIntention.FromHash($"staticscene_{SceneID}"),
                    PartitionComponent.TOP_PRIORITY);
            }

            return false;
        }

        private bool IsSupported() =>
            AssetBundleData.Exception == null;

        public void AddInstantiatedAsset(string hash, GltfContainerAsset asset)
        {
            AssetsInstantiated.Add((hash, asset));
            AllAssetsInstantiated = AssetsInstantiated.Count == AssetBundleData.Asset.InitialSceneStateMetadata.Value.assetHash.Count;
        }

        public static InitialSceneStateDescriptor CreateUnsupported(string sceneID)
        {
            InitialSceneStateDescriptor unsuportedStaticSceneAB = new InitialSceneStateDescriptor();
            unsuportedStaticSceneAB.AssetBundleData = new StreamableLoadingResult<AssetBundleData>(ReportCategory.ASSET_BUNDLES, new Exception($"Static Scene Asset Bundle not suported for {sceneID}"));
            return unsuportedStaticSceneAB;
        }

        public static InitialSceneStateDescriptor CreateSupported(World world, IGltfContainerAssetsCache assetsCache, string sceneID)
        {
            InitialSceneStateDescriptor suportedStaticSceneAB = new InitialSceneStateDescriptor();
            suportedStaticSceneAB.SceneID = sceneID;
            suportedStaticSceneAB.AssetBundlePromise = AssetBundlePromise.NULL;
            suportedStaticSceneAB.AssetsInstantiated = new List<(string,GltfContainerAsset)>();
            suportedStaticSceneAB.AssetBundleData = new ();
            suportedStaticSceneAB.GlobalWorld = world;
            suportedStaticSceneAB.assetsCache = assetsCache;
            return suportedStaticSceneAB;
        }

        public void RepositionStaticAssets(GameObject instantiatedLOD)
        {
            for (var i = 0; i < AssetBundleData.Asset.InitialSceneStateMetadata.Value.assetHash.Count; i++)
            {
                string assetHash = AssetBundleData.Asset.InitialSceneStateMetadata.Value.assetHash[i];
                if (assetsCache.TryGet(assetHash, out var asset))
                {
                    asset.Root.SetActive(true);
                    asset.Root.transform.SetParent(instantiatedLOD.transform);
                    asset.Root.transform.position = AssetBundleData.Asset.InitialSceneStateMetadata.Value.positions[i];
                    asset.Root.transform.rotation = AssetBundleData.Asset.InitialSceneStateMetadata.Value.rotations[i];
                    asset.Root.transform.localScale = AssetBundleData.Asset.InitialSceneStateMetadata.Value.scales[i];
                }
            }
        }

        public void MoveToCache()
        {
            foreach (var valueTuple in AssetsInstantiated)
            {
                valueTuple.Item2.Root.transform.SetParent(null);
                assetsCache.Dereference(valueTuple.Item1, valueTuple.Item2);
            }
        }

        public void MarkAssetToMoveToBridge()
        {
            if (!IsSupported())
                return;

            foreach ((string, GltfContainerAsset) gltfContainerAsset in AssetsInstantiated)
                gltfContainerAsset.Item2.Root.transform.SetParent(null);
        }

    }
}
