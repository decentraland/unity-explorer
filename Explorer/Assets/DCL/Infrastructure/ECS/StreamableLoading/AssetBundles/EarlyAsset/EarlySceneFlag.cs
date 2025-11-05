using DCL.Ipfs;

namespace ECS.StreamableLoading.AssetBundles.EarlyAsset
{
    public struct EarlySceneFlag
    {
    }

    public struct EarlyAssetBundleFlag
    {
        public EntityDefinitionBase Scene;

        public static EarlyAssetBundleFlag CreateAssetBundleRequest(EntityDefinitionBase scene) =>
            new ()
            {
                Scene = scene,
            };
    }
}
