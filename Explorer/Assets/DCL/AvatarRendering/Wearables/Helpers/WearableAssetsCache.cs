using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PerformanceAndDiagnostics.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

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

        private readonly SimplePriorityQueue<WearableAsset, long> unloadQueue = new ();

        public Dictionary<WearableAsset, List<CachedWearable>> Cache { get; }
        public List<CachedWearable> AllCachedWearables { get; } = new ();

        public WearableAssetsCache(int initialCapacity)
        {
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(WearableAssetsCache)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            Cache = new Dictionary<WearableAsset, List<CachedWearable>>(initialCapacity);

            // instantiate a couple of lists to prevent runtime allocations
            listPool = new ListObjectPool<CachedWearable>(defaultCapacity: initialCapacity);
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(parentContainer);
        }

        public bool TryGet(WearableAsset asset, out CachedWearable instance)
        {
            if (Cache.TryGetValue(asset, out List<CachedWearable> list) && list.Count > 0)
            {
                // Remove from the tail of the list
                instance = list[^1];
                list.RemoveAt(list.Count - 1);

                if (list.Count == 0)
                {
                    Cache.Remove(asset);
                    unloadQueue.Remove(asset);
                }
                else
                    unloadQueue.TryUpdatePriority(asset, MultithreadingUtility.FrameCount);

                ProfilingCounters.CachedWearablesInCacheAmount.Value--;
                return true;
            }

            instance = default(CachedWearable);
            return false;
        }

        public void Release(CachedWearable cachedWearable)
        {
            WearableAsset asset = cachedWearable.OriginalAsset;

            if (!Cache.TryGetValue(asset, out List<CachedWearable> list))
            {
                Cache[asset] = list = listPool.Get();
                unloadQueue.Enqueue(asset, MultithreadingUtility.FrameCount);
            }
            else
                unloadQueue.TryUpdatePriority(asset, MultithreadingUtility.FrameCount);

            list.Add(cachedWearable);

            ProfilingCounters.CachedWearablesInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (!UnityObjectUtils.IsQuitting)
            {
                cachedWearable.Instance.SetActive(false);
                cachedWearable.Instance.transform.SetParent(parentContainer);
            }
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudgetProvider.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out WearableAsset key) && Cache.TryGetValue(key, out List<CachedWearable> assets))
            {
                unloadedAmount += assets.Count;

                foreach (CachedWearable asset in assets)
                    asset.Dispose();

                assets.Clear();
                Cache.Remove(key);
            }

            ProfilingCounters.CachedWearablesInCacheAmount.Value -= unloadedAmount;
        }
    }
}
