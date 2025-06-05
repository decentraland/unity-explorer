using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Cache
{
    /// <summary>
    ///     Null implementation to provide no caching capabilities explicitly
    /// </summary>
    /// <typeparam name="TAsset"></typeparam>
    /// <typeparam name="TLoadingIntention"></typeparam>
    public class NoCache<TAsset, TLoadingIntention> : IStreamableCache<TAsset, TLoadingIntention> where TLoadingIntention: struct, ILoadingIntention, IEquatable<TLoadingIntention>
    {
        private static readonly EqualityComparer EQUALITY_COMPARER = new ();

        private static readonly DictionaryObjectPool<TLoadingIntention, UniTaskCompletionSource<OngoingRequestResult<TAsset>>>
            ONGOING_REQUEST_POOL = new (defaultCapacity: PoolConstants.SCENES_COUNT, maxSize: PoolConstants.SCENES_MAX_CAPACITY, equalityComparer: EQUALITY_COMPARER);

        private class EqualityComparer : IEqualityComparer<TLoadingIntention>
        {
            public bool Equals(TLoadingIntention x, TLoadingIntention y) =>
                EqualityComparer<TLoadingIntention>.Default.Equals(x, y);

            public int GetHashCode(TLoadingIntention obj) =>
                EqualityComparer<TLoadingIntention>.Default.GetHashCode(obj);
        }

        public static readonly NoCache<TAsset, TLoadingIntention> INSTANCE = new (false, false);

        private readonly bool useOngoingRequestCache;
        private readonly bool useIrrecoverableFailureCache;

        public IDictionary<TLoadingIntention, UniTaskCompletionSource<OngoingRequestResult<TAsset>>> OngoingRequests { get; }
        public IDictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public NoCache(bool useOngoingRequestCache, bool useIrrecoverableFailureCache)
        {
            this.useOngoingRequestCache = useOngoingRequestCache;
            this.useIrrecoverableFailureCache = useIrrecoverableFailureCache;

            if (useOngoingRequestCache)
                OngoingRequests = ONGOING_REQUEST_POOL.Get();
            else
                OngoingRequests = FakeDictionaryCache<TLoadingIntention, UniTaskCompletionSource<OngoingRequestResult<TAsset>>>.INSTANCE;

            if (useIrrecoverableFailureCache)
                IrrecoverableFailures = DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?>.Get();
            else
                IrrecoverableFailures = FakeDictionaryCache<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>?>.INSTANCE;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (useOngoingRequestCache)
                ONGOING_REQUEST_POOL.Release((Dictionary<TLoadingIntention, UniTaskCompletionSource<OngoingRequestResult<TAsset>>>)OngoingRequests);

            if (useIrrecoverableFailureCache)
                DictionaryPool<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>>.Release(IrrecoverableFailures as Dictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAsset>>);

            disposed = true;
        }

        public bool TryGet(in TLoadingIntention key, out TAsset asset)
        {
            asset = default(TAsset);
            return false;
        }

        public void Add(in TLoadingIntention key, TAsset asset) { }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }
    }
}
