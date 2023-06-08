using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle
{
    public struct ScenePointer
    {
        public readonly IpfsTypes.SceneEntityDefinition Definition;
        /// <summary>
        ///     Manifest promise is null for Empty Scenes
        /// </summary>
        public AssetPromise<SceneAssetBundleManifest, GetAssetBundleManifestIntention>? ManifestPromise;

        public ScenePointer(IpfsTypes.SceneEntityDefinition definition, AssetPromise<SceneAssetBundleManifest, GetAssetBundleManifestIntention>? manifestPromise)
        {
            Definition = definition;
            ManifestPromise = manifestPromise;
        }
    }
}
