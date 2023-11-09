using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
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
        private readonly Dictionary<WearableAsset, List<CachedWearable>> cache;
        private readonly ListObjectPool<CachedWearable> listPool;

        private readonly int maxNumberOfAssetsPerKey;
        private readonly Transform parentContainer;
        public List<CachedWearable> AllCachedWearables { get; } = new ();

        public WearableAssetsCache(int maxNumberOfAssetsPerKey, int initialCapacity)
        {
            this.maxNumberOfAssetsPerKey = maxNumberOfAssetsPerKey;

            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(WearableAssetsCache)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            cache = new Dictionary<WearableAsset, List<CachedWearable>>(initialCapacity);

            // instantiate a couple of lists to prevent runtime allocations
            listPool = new ListObjectPool<CachedWearable>(listInstanceDefaultCapacity: maxNumberOfAssetsPerKey, defaultCapacity: initialCapacity);
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(parentContainer);
        }

        public bool TryGet(WearableAsset asset, out CachedWearable instance)
        {
            if (cache.TryGetValue(asset, out List<CachedWearable> list) && list.Count > 0)
            {
                // Remove from the tail of the list
                instance = list[^1];
                list.RemoveAt(list.Count - 1);
                ProfilingCounters.WearablesAmountCacheSize.Value--;

                if (list.Count == 0)
                    cache.Remove(asset);

                return true;
            }

            instance = default(CachedWearable);
            return false;
        }

        public IWearableAssetsCache.ReleaseResult TryRelease(CachedWearable cachedWearable)
        {
            WearableAsset asset = cachedWearable.OriginalAsset;

            if (!cache.TryGetValue(asset, out List<CachedWearable> list))
                cache[asset] = list = listPool.Get();

            if (list.Count >= maxNumberOfAssetsPerKey)
                return IWearableAssetsCache.ReleaseResult.CapacityExceeded;

            list.Add(cachedWearable);
            ProfilingCounters.WearablesAmountCacheSize.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return IWearableAssetsCache.ReleaseResult.EnvironmentIsDisposing;

            cachedWearable.Instance.SetActive(false);
            cachedWearable.Instance.transform.SetParent(parentContainer);
            return IWearableAssetsCache.ReleaseResult.ReturnedToPool;
        }

        public void UnloadCachedWearables()
        {
            foreach (List<CachedWearable> cachedWearablesList in cache.Values)
            {
                ProfilingCounters.WearablesAmountCacheSize.Value -= cachedWearablesList.Count;

                foreach (CachedWearable cachedWearable in cachedWearablesList)
                    cachedWearable.Dispose();

                cachedWearablesList.Clear();
            }

            // foreach (CachedWearable cachedWearable in AllCachedWearables)
            //     cachedWearable.Dispose();
            //
            // AllCachedWearables.Clear();
        }

        public bool TryUnloadCacheKey(WearableAsset wearableAsset)
        {
            if (!cache.ContainsKey(wearableAsset)) return false;
            if (wearableAsset is not { ReferenceCount: 0 }) return false;

            cache.Remove(wearableAsset);
            wearableAsset.Dispose();
            return true;
        }
    }
}
