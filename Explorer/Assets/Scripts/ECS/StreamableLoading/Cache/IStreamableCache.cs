using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    public interface IStreamableCache<TAsset, TLoadingIntention> : IEqualityComparer<TLoadingIntention>
    {
        /// <summary>
        ///     Resolves the problem of having multiple requests to the same URL at a time,
        ///     should be shared across multiple scenes as their assets can be shared as well
        /// </summary>
        IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; }

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
