using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.GLTFContainer.Asset.Components;
using Global;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.Unity.GLTFContainer.Asset.Cache
{
    /// <summary>
    ///     Individual pool for each GltfContainer source. LRU cache
    ///     <para>Gltf Containers can't be reused</para>
    /// </summary>
    public class GltfContainerAssetsCache : IStreamableCache<GltfContainerAsset, string>
    {
        private readonly Dictionary<string, (CacheMetrics metrics, List<GltfContainerAsset> assets)> cache;

        private readonly int maxSize;

        private readonly Transform parentContainer;

        private readonly HashSet<string> cacheKeysToUnload;
        private readonly IConcurrentBudgetProvider frameBudget;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<GltfContainerAsset>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public GltfContainerAssetsCache(int maxSize, IConcurrentBudgetProvider frameBudget)
        {
            this.frameBudget = frameBudget;
            GameStats.GLTFCacheSizeCounter.Value = 0;

            this.maxSize = Mathf.Min(500, maxSize);
            cache = new Dictionary<string, (CacheMetrics, List<GltfContainerAsset>)>(this.maxSize, this);
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(GltfContainerAsset)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            OngoingRequests = new FakeDictionaryCache<UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>>();
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<GltfContainerAsset>>.Get();
            cacheKeysToUnload = HashSetPool<string>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            disposed = true;
        }

        public bool TryGet(in string key, out GltfContainerAsset asset)
        {
            if (cache.TryGetValue(key, out (CacheMetrics metrics, List<GltfContainerAsset> list) cacheValue) && cacheValue.list.Count > 0)
            {
                // Remove from the tail of the list
                asset = cacheValue.list[^1];
                cacheValue.list.RemoveAt(cacheValue.list.Count - 1);

                cacheValue.metrics.ReusedCount++;
                cacheValue.metrics.LastUsedTime = Time.unscaledTime;

                GameStats.GLTFCacheSizeCounter.Value = cache.Count;
                return true;
            }

            asset = default(GltfContainerAsset);
            return false;
        }

        public void Add(in string key, GltfContainerAsset asset)
        {
            // Nothing to do, we don't reuse the existing instantiated game objects
        }

        public void Dereference(in string key, GltfContainerAsset asset)
        {
            // Return to the pool
            if (!cache.TryGetValue(key, out (CacheMetrics metrics, List<GltfContainerAsset> list) cacheValue))
            {
                cacheValue.metrics = new CacheMetrics(Time.unscaledTime, 0);
                cacheValue.list = new List<GltfContainerAsset>(maxSize / 10);
                cache[key] = cacheValue;
            }

            cacheValue.metrics.LastUsedTime = Time.unscaledTime;
            cacheValue.list.Add(asset);
            GameStats.GLTFCacheSizeCounter.Value = cache.Count;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer);
        }

        public int UnloadUnusedCache(int budget)
        {
            foreach (KeyValuePair<string, (CacheMetrics metrics, List<GltfContainerAsset> assets)> entry in cache)
            {
                CacheMetrics cacheMetrics = entry.Value.metrics;

                if (Time.unscaledTime - cacheMetrics.LastUsedTime > CacheCleaner.CACHE_EXPIRATION_TIME)
                {
                    var i = 0;

                    for (; i < entry.Value.assets.Count; i++)
                    {
                        if (!frameBudget.TrySpendBudget() || cacheKeysToUnload.Count > budget) break;
                        entry.Value.assets[i].Dispose();
                    }

                    if (i == entry.Value.assets.Count)
                        cacheKeysToUnload.Add(entry.Key);
                }
                else
                {
                    // leave only 1 asset for specific key
                    for (var i = 0; i < entry.Value.assets.Count - 1; i++)
                    {
                        if (!frameBudget.TrySpendBudget() || cacheKeysToUnload.Count > budget) break;
                        entry.Value.assets[i].Dispose();
                    }
                }
            }

            foreach (string key in cacheKeysToUnload)
                cache.Remove(key);

            int unloadedCount = cacheKeysToUnload.Count;
            cacheKeysToUnload.Clear();

            return unloadedCount;
        }

        bool IEqualityComparer<string>.Equals(string x, string y) =>
            string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        int IEqualityComparer<string>.GetHashCode(string obj) =>
            obj.GetHashCode(StringComparison.OrdinalIgnoreCase);

        private class CacheMetrics
        {
            public int ReusedCount;
            public float LastUsedTime;

            public CacheMetrics(float lastUsedTime, int reusedCount)
            {
                LastUsedTime = lastUsedTime;
                ReusedCount = reusedCount;
            }
        }
    }
}
