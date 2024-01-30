namespace ECS.StreamableLoading.Cache
{
    public interface IDereferencableCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention>
    {
        /// <summary>
        ///     Signal the cache that a single usage of asset went out of scope.
        ///     It is needed for cache with limited capacity based on LRU, reference counting
        /// </summary>
        void Dereference(in TLoadingIntention key, TAsset asset);
    }
}
