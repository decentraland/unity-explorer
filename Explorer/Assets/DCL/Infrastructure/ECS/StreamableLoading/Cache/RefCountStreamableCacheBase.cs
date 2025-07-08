using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace ECS.StreamableLoading.Cache
{
    public abstract class RefCountStreamableCacheBase<TAssetData, TAsset, TLoadingIntention> : IStreamableCache<TAssetData, TLoadingIntention>
        where TAssetData: StreamableRefCountData<TAsset> where TAsset: class where TLoadingIntention: IEquatable<TLoadingIntention>
    {
        private static readonly Comparison<(TLoadingIntention intention, TAssetData asset)> COMPARE_BY_LAST_USED_FRAME_REVERSED =
            (d1, d2) => d2.asset.LastUsedFrame.CompareTo(d1.asset.LastUsedFrame);

        internal readonly Dictionary<TLoadingIntention, TAssetData> cache = new (IntentionsComparer<TLoadingIntention>.INSTANCE);

        internal readonly List<(TLoadingIntention intention, TAssetData asset)> listedCache = new ();

        private bool disposed;

        public IDictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<TAssetData>>> OngoingRequests { get; }
            = new Dictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<TAssetData>>>();

        public IDictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAssetData>?> IrrecoverableFailures { get; }
            = new Dictionary<IntentionsComparer<TLoadingIntention>.SourcedIntentionId, StreamableLoadingResult<TAssetData>?>();

        protected abstract ref ProfilerCounterValue<int> inCacheCount { get; }

        public void Dispose()
        {
            if (disposed) return;

            foreach (TAssetData? assetData in cache.Values)
                assetData.Dispose(true);

            IrrecoverableFailures.Clear();
            OngoingRequests.Clear();
            cache.Clear();
            listedCache.Clear();

            inCacheCount.Value = 0;
            disposed = true;
        }

        public void Add(in TLoadingIntention key, TAssetData asset)
        {
            if (cache.TryAdd(key, asset))
                listedCache.Add((key, asset));

            inCacheCount.Value = cache.Count;
        }

        public bool TryGet(in TLoadingIntention key, out TAssetData asset) =>
            cache.TryGetValue(key, out asset);

        public void AddReference(in TLoadingIntention _, TAssetData asset)
        {
            asset.AddReference();
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            listedCache.Sort(COMPARE_BY_LAST_USED_FRAME_REVERSED);

            for (int i = listedCache.Count - 1; frameTimeBudget.TrySpendBudget() && i >= 0 && maxUnloadAmount > 0; i--)
            {
                (var key, var asset) = listedCache[i];
                if (!asset.CanBeDisposed()) continue;

                asset.Dispose();
                cache.Remove(key);
                listedCache.RemoveAt(i);

                maxUnloadAmount--;
            }

            inCacheCount.Value = cache.Count;
        }
    }
}
