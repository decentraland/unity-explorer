using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle
{
    public class ScenePointer
    {
        public readonly IpfsTypes.SceneEntityDefinition Definition;
        public readonly bool IsEmpty;

        /// <summary>
        ///     Manifest promise is null for Empty Scenes
        /// </summary>
        public AssetPromise<SceneAssetBundleManifest, GetAssetBundleManifestIntention> ManifestPromise;

        public ScenePointer(IpfsTypes.SceneEntityDefinition definition, AssetPromise<SceneAssetBundleManifest, GetAssetBundleManifestIntention> manifestPromise)
        {
            Definition = definition;
            ManifestPromise = manifestPromise;
            IsEmpty = false;
        }

        /// <summary>
        ///     Create empty scene pointer
        /// </summary>
        /// <param name="parcel"></param>
        public ScenePointer(Vector2Int parcel)
        {
            IsEmpty = true;

            Definition = new IpfsTypes.SceneEntityDefinition
            {
                id = $"empty-parcel-{parcel.x}-{parcel.y}",
            };
        }
    }
}
