using Arch.Core;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
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
        public bool HasProperABManifestVersion { get; private set; }

        private List<(string, GltfContainerAsset)> AssetsInstantiated;
        private World GlobalWorld;
        private string SceneID;
        private AssetBundleManifestVersion AssetBundleManifestVersion;
        private IGltfContainerAssetsCache assetsCache;

        private bool AllAssetsInstantiated;

        public bool IsReady()
        {
            if (!HasProperABManifestVersion)
                return true;

            //The asset bundle failed to load for some reason...this is an escape route. The scene load will fail,
            //abs seems to be corrupt
            if (AssetBundleData.Exception != null)
                return true;

            //The asset bundle was destroyed at some point because of memory constrains. We got to nullify it and restart
            if (AssetBundleData.IsInitialized && AssetBundleData.Asset!.AssetsDestroyed)
            {
                CreateEmptyAssetBundleData();
                return false;
            }

            if (AssetBundleData.IsInitialized && AllAssetsInstantiated)
                return true;

            if (!AssetBundleData.IsInitialized && AssetBundlePromise == AssetBundlePromise.NULL)
            {
                //TOO (JUANI): Here we will use the sceneID to create the promise
                AssetBundlePromise = AssetBundlePromise.Create(GlobalWorld,
                    GetAssetBundleIntention.FromHash($"staticscene_{SceneID}{PlatformUtils.GetCurrentPlatform()}",
                        assetBundleManifestVersion: AssetBundleManifestVersion,
                        parentEntityID: SceneID),
                    PartitionComponent.TOP_PRIORITY);
            }

            return false;
        }

        public void AddInstantiatedAsset(string hash, GltfContainerAsset asset)
        {
            AssetsInstantiated.Add((hash, asset));
            AllAssetsInstantiated = AssetsInstantiated.Count == AssetBundleData.Asset.InitialSceneStateMetadata.Value.assetHash.Count;
        }

        public static InitialSceneStateDescriptor CreateUnsupported(string sceneID)
        {
            InitialSceneStateDescriptor unsuportedStaticSceneAB = new InitialSceneStateDescriptor();
            unsuportedStaticSceneAB.AssetBundleData = new StreamableLoadingResult<AssetBundleData>(ReportCategory.ASSET_BUNDLES, new Exception($"Static Scene Asset Bundle not suported for {sceneID}"));
            unsuportedStaticSceneAB.AssetsInstantiated = new List<(string,GltfContainerAsset)>();
            unsuportedStaticSceneAB.HasProperABManifestVersion = false;
            return unsuportedStaticSceneAB;
        }

        public static InitialSceneStateDescriptor CreateSupported(World world, IGltfContainerAssetsCache assetsCache, EntityDefinitionBase entityDefinition)
        {
            InitialSceneStateDescriptor suportedStaticSceneAB = new InitialSceneStateDescriptor();
            suportedStaticSceneAB.HasProperABManifestVersion = true;
            suportedStaticSceneAB.SceneID = entityDefinition.id;
            suportedStaticSceneAB.AssetBundleManifestVersion = entityDefinition.assetBundleManifestVersion;

            suportedStaticSceneAB.GlobalWorld = world;
            suportedStaticSceneAB.assetsCache = assetsCache;
            suportedStaticSceneAB.CreateEmptyAssetBundleData();
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

        /// <summary>
        /// Returns if the assets could be moves to the GLTFAssetCache
        /// </summary>
        /// <param name="bridgingBetweenScene"></param>
        /// <returns></returns>
        public void AnalyzeCacheState(bool bridgingBetweenScene, bool assetsAreInUse)
        {
            foreach (var valueTuple in AssetsInstantiated)
            {
                if (bridgingBetweenScene)
                    assetsCache.PutInBridge(valueTuple.Item2);

                if (assetsAreInUse)
                    assetsCache.Dereference(valueTuple.Item1, valueTuple.Item2);
            }
        }

        public void MarkAssetToMoveToBridge()
        {
            foreach ((string, GltfContainerAsset) gltfContainerAsset in AssetsInstantiated)
                assetsCache.PutInBridge(gltfContainerAsset.Item2);
        }


        private void CreateEmptyAssetBundleData()
        {
            AssetBundlePromise = AssetBundlePromise.NULL;
            AssetsInstantiated = new List<(string, GltfContainerAsset)>();
            AssetBundleData = new StreamableLoadingResult<AssetBundleData>();
        }

        public bool AssetBundleFailed()
        {
            return AssetBundleData.Exception != null;
        }
    }
}
