using ECS.StreamableLoading.Common.Components;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    public struct GetAssetBundleManifestIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly string SceneId;

        public GetAssetBundleManifestIntention(string sceneId) : this()
        {
            SceneId = sceneId;
        }
    }
}
