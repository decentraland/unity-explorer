using DCL.Profiling;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ListObjectPool<CachedWearable> listPool;

        private readonly Transform parentContainer;
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
                ProfilingCounters.CachedWearablesInCacheAmount.Value--;

                if (list.Count == 0)
                    Cache.Remove(asset);

                return true;
            }

            instance = default(CachedWearable);
            return false;
        }

        public IWearableAssetsCache.ReleaseResult TryRelease(CachedWearable cachedWearable)
        {
            WearableAsset asset = cachedWearable.OriginalAsset;

            if (!Cache.TryGetValue(asset, out List<CachedWearable> list))
                Cache[asset] = list = listPool.Get();

            list.Add(cachedWearable);
            ProfilingCounters.CachedWearablesInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return IWearableAssetsCache.ReleaseResult.EnvironmentIsDisposing;

            cachedWearable.Instance.SetActive(false);
            cachedWearable.Instance.transform.SetParent(parentContainer);
            return IWearableAssetsCache.ReleaseResult.ReturnedToPool;
        }

        public void UnloadCachedWearables()
        {
            foreach (List<CachedWearable> cachedWearablesList in Cache.Values)
            {
                ProfilingCounters.CachedWearablesInCacheAmount.Value -= cachedWearablesList.Count;

                foreach (CachedWearable cachedWearable in cachedWearablesList)
                    cachedWearable.Dispose();

                cachedWearablesList.Clear();
            }

            // foreach (CachedWearable cachedWearable in AllCachedWearables)
            //     cachedWearable.Dispose();
            //
            // AllCachedWearables.Clear();
        }

        public void UnloadCachedWearablesKeys()
        {
            var keysToRemove = Cache.Keys.Where(wearablesAsset => wearablesAsset == null).ToList();

            foreach (WearableAsset key in keysToRemove)
                Cache.Remove(key);

            keysToRemove.Clear();
        }
    }
}
