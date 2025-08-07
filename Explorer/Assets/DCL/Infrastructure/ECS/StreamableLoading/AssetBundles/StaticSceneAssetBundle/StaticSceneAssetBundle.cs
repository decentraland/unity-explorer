using ECS.StreamableLoading.Common.Components;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class StaticSceneAssetBundle
    {
        public bool Supported;
        public bool Request;

        public StreamableLoadingResult<AssetBundleData> AssetBundleData = new ();
        public AssetBundlePromise AssetBundlePromise = AssetBundlePromise.NULL;

        public StaticSceneDescriptor StaticSceneDescriptor;

        public void RequestAssetBundle()
        {
            Request = true;
        }

    }
}
