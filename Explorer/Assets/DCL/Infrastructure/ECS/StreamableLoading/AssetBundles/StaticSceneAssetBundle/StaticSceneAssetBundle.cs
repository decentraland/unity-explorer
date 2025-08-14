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

namespace ECS.StreamableLoading.AssetBundles
{
    public class StaticSceneAssetBundle
    {
        public StreamableLoadingResult<AssetBundleData> AssetBundleData;
        public AssetBundlePromise AssetBundlePromise;
        public List<(string, GltfContainerAsset)> AssetsInstantiated;

        private World GlobalWorld;
        private string SceneID;
        private IGltfContainerAssetsCache assetsCache;

        private bool AllAssetsInstantiated;

        private StaticSceneAssetBundle() { }


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
                    GetAssetBundleIntention.CreateSingleAssetBundleHack("GP_staticscene_LZMA_StaticSceneDescriptor"),
                    PartitionComponent.TOP_PRIORITY);
            }

            return false;
        }

        private bool IsSupported() =>
            AssetBundleData.Exception == null;

        public void AddInstantiatedAsset(string hash, GltfContainerAsset asset)
        {
            AssetsInstantiated.Add((hash, asset));
            AllAssetsInstantiated = AssetsInstantiated.Count == AssetBundleData.Asset.StaticSceneDescriptor.assetHash.Count;
        }

        public static StaticSceneAssetBundle CreateUnsupported(string sceneID)
        {
            StaticSceneAssetBundle unsuportedStaticSceneAB = new StaticSceneAssetBundle();
            unsuportedStaticSceneAB.AssetBundleData = new StreamableLoadingResult<AssetBundleData>(ReportCategory.ASSET_BUNDLES, new Exception($"Static Scene Asset Bundle not suported for {sceneID}"));
            return unsuportedStaticSceneAB;
        }

        public static StaticSceneAssetBundle CreateSupported(World world, IGltfContainerAssetsCache assetsCache)
        {
            StaticSceneAssetBundle suportedStaticSceneAB = new StaticSceneAssetBundle();
            suportedStaticSceneAB.AssetBundlePromise = AssetBundlePromise.NULL;
            suportedStaticSceneAB.AssetsInstantiated = new List<(string,GltfContainerAsset)>();
            suportedStaticSceneAB.AssetBundleData = new ();
            suportedStaticSceneAB.GlobalWorld = world;
            suportedStaticSceneAB.assetsCache = assetsCache;
            return suportedStaticSceneAB;
        }

        public void RepositionStaticAssets(GameObject instantiatedLOD)
        {
            for (var i = 0; i < AssetBundleData.Asset.StaticSceneDescriptor.assetHash.Count; i++)
            {
                string assetHash = AssetBundleData.Asset.StaticSceneDescriptor.assetHash[i];
                if (assetsCache.TryGet(assetHash, out var asset))
                {
                    asset.Root.SetActive(true);
                    asset.Root.transform.SetParent(instantiatedLOD.transform);
                    asset.Root.transform.position = AssetBundleData.Asset.StaticSceneDescriptor.positions[i];
                    asset.Root.transform.rotation = AssetBundleData.Asset.StaticSceneDescriptor.rotations[i];
                    asset.Root.transform.localScale = AssetBundleData.Asset.StaticSceneDescriptor.scales[i];
                }
            }
        }

        public void MoveToCache()
        {
            foreach (var valueTuple in AssetsInstantiated)
            {
                valueTuple.Item2.Scene_LOD_Bridge_Asset = true;
                assetsCache.Dereference(valueTuple.Item1, valueTuple.Item2);
            }
        }

        public void MarkAssetToMoveToBridge()
        {
            if (!IsSupported())
                return;

            foreach ((string, GltfContainerAsset) gltfContainerAsset in AssetsInstantiated)
                gltfContainerAsset.Item2.Scene_LOD_Bridge_Asset = true;
        }
    }
}
