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

        public StaticSceneDescriptor StaticSceneDescriptor;
        public World GlobalWorld;
        public string SceneID;

        public StaticSceneAssetBundle(World globalWorld, string sceneID)
        {
            StaticSceneDescriptor = null;
            GlobalWorld = globalWorld;
            SceneID = sceneID;
            AssetBundlePromise = AssetBundlePromise.NULL;
            SceneID = sceneID;


            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            if (!SceneID.Equals("bafkreifqcraqxctg4krbklm6jsbq2x5tueevhmvxx354obl4ogu5owkbqu"))
                AssetBundleData = new StreamableLoadingResult<AssetBundleData>(ReportCategory.ASSET_BUNDLES, new Exception($"Static Scene Asset Bundle not suported for {SceneID}"));
            else
                AssetBundleData = new ();
        }

        public bool IsReady()
        {
            if (!IsSupported())
                return true;

            if (AssetBundleData.IsInitialized)
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

        public bool IsSupported() =>
            AssetBundleData.Exception == null;
    }
}
