using ECS.StreamableLoading.AssetBundles;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.ECS.StreamableLoading.AssetBundles
{
    public struct StaticScene
    {
        public AssetBundlePromise StaticSceneAssetBundlePromise;
        public bool IsReady;
        public AssetBundleData AssetData;
    }
}
