using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Cache;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Asset.Cache
{
    /// <summary>
    /// Has nothing to do with hierarchy of <see cref="IStreamableCache{TAsset,TLoadingIntention}"/>
    /// </summary>
    public interface IGltfContainerAssetsCache
    {
        bool TryGet(in string key, out GltfContainerAsset? asset);

        void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount);

        /// <summary>
        ///     Evict a single cache key, disposing every pooled asset under it. Lets an edited GLTF be
        ///     dropped in isolation while the rest of the cache stays warm across a scene reload.
        /// </summary>
        void Remove(in string key);

        void Dereference(in string key, GltfContainerAsset asset, bool putInBridge = false, bool handleAssetLoad = true);

        void SetAssetLoadCache(AssetPreLoadCache assetPreLoadCache);

    }
}
