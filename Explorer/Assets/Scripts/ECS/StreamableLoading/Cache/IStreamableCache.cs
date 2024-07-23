using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Streamable Cache is shared between multiple instances of <see cref="LoadSystemBase{TAsset,TIntention}" /> with the same arguments
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public interface IStreamableCache<TAsset, TLoadingIntention> : IEqualityComparer<TLoadingIntention>, IDisposable
    {
        IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; }
        IDictionary<string, StreamableLoadingResult<TAsset>> IrrecoverableFailures { get; }

        /// <summary>
        ///     Get the asset for referencing, it should be called one time and saved in the component,
        ///     it is a signal to increase reference count but ref count should not be increased in this method
        /// </summary>
        /// <returns></returns>
        [Pure] bool TryGet(in TLoadingIntention key, out TAsset asset);

        /// <summary>
        ///     Add is supposed to be called only once per unique asset and should not be
        ///     connected to the ref count increasing
        /// </summary>
        /// <param name="key"></param>
        /// <param name="asset"></param>
        void Add(in TLoadingIntention key, TAsset asset);

        /// <summary>
        ///     Unload assets from the cache to free memory
        /// </summary>
        void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount);

#region Fake
        class Fake : IStreamableCache<TAsset, TLoadingIntention>
        {
            public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>> OngoingRequests { get; } = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<TAsset>?>>();
            public IDictionary<string, StreamableLoadingResult<TAsset>> IrrecoverableFailures { get; } = new Dictionary<string, StreamableLoadingResult<TAsset>>();

            public void Dispose() { }

            public bool Equals(TLoadingIntention x, TLoadingIntention y) =>
                throw new Exception("I am fake, try replace me with a real implementation");

            public int GetHashCode(TLoadingIntention obj) =>
                throw new Exception("I am fake, try replace me with a real implementation");

            public bool TryGet(in TLoadingIntention key, out TAsset asset)
            {
                asset = default(TAsset);
                return false;
            }

            public void Add(in TLoadingIntention key, TAsset asset)
            {
                //ignore
            }

            public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
            {
                //ignore
            }
        }
#endregion

#region Referencing
        /// <summary>
        ///     Base implementation is empty as not every asset requires reference counting
        /// </summary>
        void AddReference(in TLoadingIntention key, TAsset asset) { }

        /// <summary>
        ///     Signal the cache that a single usage of asset went out of scope.
        ///     It is needed for cache with limited capacity based on LRU, reference counting
        /// </summary>
        void Dereference(in TLoadingIntention key, TAsset asset) { }
#endregion
    }
}
