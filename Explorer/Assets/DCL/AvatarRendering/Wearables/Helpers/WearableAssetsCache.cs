using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     <para>
    ///         This class is used to store instances of wearable assets
    ///     </para>
    ///     <para>
    ///         It keeps a limited reasonable number of unique assets
    ///     </para>
    /// </summary>
    public class WearableAssetsCache : IWearableAssetsCache, IDisposable
    {
        // string is hash here which is retrieved via IWearable.GetMainFileHash
        private readonly ListObjectPool<CachedWearable> listPool;

        private readonly Transform parentContainer;

        public Dictionary<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> Cache { get; }
        public List<CachedWearable> AllCachedWearables { get; } = new ();

        public WearableAssetsCache(int initialCapacity)
        {
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(WearableAssetsCache)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            Cache = new Dictionary<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)>(initialCapacity);

            // instantiate a couple of lists to prevent runtime allocations
            listPool = new ListObjectPool<CachedWearable>(defaultCapacity: initialCapacity);
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(parentContainer);
        }

        public bool TryGet(WearableAsset asset, out CachedWearable instance)
        {
            if (Cache.TryGetValue(asset, out (uint LastUsedFrame, List<CachedWearable> list) value) && value.list.Count > 0)
            {
                // Remove from the tail of the list
                instance = value.list[^1];

                value.list.RemoveAt(value.list.Count - 1);
                value.LastUsedFrame = (uint)Time.frameCount;

                if (value.list.Count == 0)
                    Cache.Remove(asset);

                ProfilingCounters.CachedWearablesInCacheAmount.Value--;
                return true;
            }

            instance = default(CachedWearable);
            return false;
        }

        public IWearableAssetsCache.ReleaseResult TryRelease(CachedWearable cachedWearable)
        {
            WearableAsset asset = cachedWearable.OriginalAsset;

            if (!Cache.TryGetValue(asset, out (uint LastUsedFrame, List<CachedWearable> list) value))
                Cache[asset] = value = ((uint)Time.frameCount, listPool.Get());

            value.list.Add(cachedWearable);
            ProfilingCounters.CachedWearablesInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return IWearableAssetsCache.ReleaseResult.EnvironmentIsDisposing;

            cachedWearable.Instance.SetActive(false);
            cachedWearable.Instance.transform.SetParent(parentContainer);
            return IWearableAssetsCache.ReleaseResult.ReturnedToPool;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            using (ListPool<KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)>>.Get(out List<KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)>> sortedCache))
            {
                PrepareListSortedByLastUsage(sortedCache);
                var totalUnloadedAssets = 0;

                foreach (KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> pair in sortedCache)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;
                    if (maxUnloadAmount-- <= 0) break;

                    int disposedGltfAssets = DisposeAssetsInSortedList(pair);
                    ClearCache(pair, disposedGltfAssets);

                    totalUnloadedAssets += disposedGltfAssets;
                }

                ProfilingCounters.CachedWearablesInCacheAmount.Value -= totalUnloadedAssets;
            }

            return;

            void PrepareListSortedByLastUsage(List<KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)>> sortedCache)
            {
                foreach (KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> item in Cache)
                    sortedCache.Add(item);

                sortedCache.Sort(CompareByLastUsedFrame);
            }

            int DisposeAssetsInSortedList(KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> pair)
            {
                var i = 0;

                for (; i < pair.Value.Assets.Count; i++)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;

                    pair.Value.Assets[i].Dispose();
                }

                return i;
            }

            void ClearCache(KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> pair, int disposedAssets)
            {
                Cache[pair.Key].Assets.RemoveRange(0, disposedAssets);

                if (Cache[pair.Key].Assets.Count == 0)
                    Cache.Remove(pair.Key);
            }
        }

        private static int CompareByLastUsedFrame(KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> pair1, KeyValuePair<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> pair2) =>
            pair1.Value.LastUsedFrame.CompareTo(pair2.Value.LastUsedFrame);
    }
}
