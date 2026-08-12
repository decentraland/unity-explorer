using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace ECS.StreamableLoading.Cache
{
    public abstract class RefCountStreamableCacheBase<TAssetData, TAsset, TLoadingIntention> : IStreamableCache<TAssetData, TLoadingIntention>
        where TAssetData: StreamableRefCountData<TAsset> where TLoadingIntention: IEquatable<TLoadingIntention>
    {
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
            // CacheCleaner.UnloadCache evicts a small fixed per-frame chunk (TEXTURE 1, GLTF 3, AUDIO_CLIP 100),
            // always tiny next to the resident count N, so a full O(N log N) sort every frame is wasted work:
            // select the k = maxUnloadAmount least-recently-used disposable entries via k linear min-scans instead.
            if (maxUnloadAmount <= 0 || listedCache.Count == 0)
                return;

            // Full-purge fast-track when maxUnloadAmount >= listedCache.Count (k >= N; the unbudgeted hot-reload
            // path passes int.MaxValue). Every disposable entry is evicted regardless of order, so a single O(N)
            // compaction sweep drains them instead of one O(N) min-scan per eviction (which would be O(N^2)).
            if (maxUnloadAmount >= listedCache.Count)
            {
                var write = 0;
                var budgetExhausted = false;

                for (var read = 0; read < listedCache.Count; read++)
                {
                    (TLoadingIntention key, TAssetData asset) = listedCache[read];

                    if (!budgetExhausted && asset.CanBeDisposed())
                    {
                        if (frameTimeBudget.TrySpendBudget())
                        {
                            asset.Dispose();
                            cache.Remove(key);
                            continue; // drop it — do not copy into the survivor prefix
                        }

                        budgetExhausted = true; // out of budget: keep this and every remaining entry
                    }

                    listedCache[write++] = listedCache[read];
                }

                listedCache.RemoveRange(write, listedCache.Count - write);

                inCacheCount.Value = cache.Count;
                return;
            }

            while (maxUnloadAmount > 0 && listedCache.Count > 0 && frameTimeBudget.TrySpendBudget())
            {
                int victim = -1;
                long victimFrame = long.MaxValue;

                for (var i = 0; i < listedCache.Count; i++)
                {
                    TAssetData asset = listedCache[i].asset;

                    if (!asset.CanBeDisposed()) continue;

                    long frame = asset.LastUsedFrame;

                    if (victim == -1 || frame < victimFrame)
                    {
                        victim = i;
                        victimFrame = frame;
                    }
                }

                if (victim == -1) break;

                (var key, TAssetData victimAsset) = listedCache[victim];

                victimAsset.Dispose();
                cache.Remove(key);

                // Order no longer needs to be preserved (nothing reads listedCache ordered), so swap-remove is O(1).
                int last = listedCache.Count - 1;
                listedCache[victim] = listedCache[last];
                listedCache.RemoveAt(last);

                maxUnloadAmount--;
            }

            inCacheCount.Value = cache.Count;
        }

        public void Remove(in TLoadingIntention key)
        {
            if (!cache.TryGetValue(key, out TAssetData asset) || !asset.CanBeDisposed())
                return;

            asset.Dispose();
            cache.Remove(key);

            for (int i = listedCache.Count - 1; i >= 0; i--)
                if (IntentionsComparer<TLoadingIntention>.INSTANCE.Equals(listedCache[i].intention, key))
                {
                    listedCache.RemoveAt(i);
                    break;
                }

            inCacheCount.Value = cache.Count;
        }
    }
}
