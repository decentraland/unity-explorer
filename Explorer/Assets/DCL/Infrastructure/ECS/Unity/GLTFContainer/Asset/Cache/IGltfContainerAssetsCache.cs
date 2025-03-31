using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Cache;
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

        void Dereference(in string key, GltfContainerAsset asset);
    }
}
