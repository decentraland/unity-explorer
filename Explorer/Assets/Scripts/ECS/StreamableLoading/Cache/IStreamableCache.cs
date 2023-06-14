using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public interface IStreamableCache<TAsset, TLoadingIntention> : IEqualityComparer<TLoadingIntention>
    {
        /// <summary>
        ///     Get the asset for referencing, it should be called one time and saved in the component,
        ///     it is a signal to increase reference count
        /// </summary>
        /// <returns></returns>
        bool TryGet(in TLoadingIntention key, out TAsset asset);

        void Add(in TLoadingIntention key, TAsset asset);

        /// <summary>
        ///     Signal the cache that a single usage of asset went out of scope.
        ///     It is needed for cache with limited capacity based on LRU, reference counting
        /// </summary>
        void Dereference(in TLoadingIntention key, TAsset asset);
    }
}
