using Arch.Core;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class StaticSceneAssetBundle
    {
        public StreamableLoadingResult<AssetBundleData> AssetBundleData;
        public AssetBundlePromise AssetBundlePromise;

        public World GlobalWorld;
        public string SceneID;
        public int AssetsInstantiated;

        public StaticSceneAssetBundle(World globalWorld, string sceneID)
        {
            GlobalWorld = globalWorld;
            SceneID = sceneID;
            AssetBundlePromise = AssetBundlePromise.NULL;
            SceneID = sceneID;

            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            if (!SceneID.Equals("bafkreicboazl7vyrwx7xujne53e63di6khbcfoi4vabafomar4u5mznpzy"))
                AssetBundleData = new StreamableLoadingResult<AssetBundleData>(ReportCategory.ASSET_BUNDLES, new Exception($"Static Scene Asset Bundle not suported for {SceneID}"));
            else
                AssetBundleData = new ();
        }

        public bool IsReady()
        {
            if (!IsSupported())
                return true;

            if (AssetBundleData.IsInitialized)
            {
                if (AssetBundleData.Asset.StaticSceneDescriptor.assetHash.Count == AssetsInstantiated)
                    return true;
                return false;
            }

            if (!AssetBundleData.IsInitialized && AssetBundlePromise == AssetBundlePromise.NULL)
            {
                //TOO (JUANI): Here we will use the sceneID to create the promise
                AssetBundlePromise = AssetBundlePromise.Create(GlobalWorld,
                    GetAssetBundleIntention.CreateSingleAssetBundleHack("GP_staticscene_LZMA_StaticSceneDescriptor"),
                    PartitionComponent.TOP_PRIORITY);
            }

            return false;
        }

        public bool IsSupported() =>
            AssetBundleData.Exception == null;
    }
}
