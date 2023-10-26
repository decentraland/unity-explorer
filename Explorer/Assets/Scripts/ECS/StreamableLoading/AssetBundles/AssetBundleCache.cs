using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Global;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : IStreamableCache<AssetBundleData, GetAssetBundleIntention>
    {
        private readonly IConcurrentBudgetProvider frameBudget;
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleCacheData> cache;
        private readonly HashSet<GetAssetBundleIntention> cacheKeysToUnload;

        private Func<AssetBundleCacheData, bool> unloadCacheFilter;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache(IConcurrentBudgetProvider frameBudget)
        {
            this.frameBudget = frameBudget;

            cache = new Dictionary<GetAssetBundleIntention, AssetBundleCacheData>(256, this);
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Get();
            cacheKeysToUnload = HashSetPool<GetAssetBundleIntention>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            foreach (AssetBundleCacheData ab in cache.Values)
                ab.Data.Dispose();

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>);
            HashSetPool<GetAssetBundleIntention>.Release(cacheKeysToUnload);

            disposed = true;
        }

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset)
        {
            asset = null;

            if (cache.TryGetValue(key, out AssetBundleCacheData cacheData))
            {
                cacheData.ReusedCount++;
                cacheData.LastUsedTime = Time.unscaledTime;

                asset = cacheData.Data;
                return true;
            }

            return false;
        }

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, new AssetBundleCacheData(asset));

            GameStats.ABCacheSizeCounter.Value = cache.Count;
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }

        public int UnloadAllCache(int budget)
        {
            unloadCacheFilter = _ => true;

            return UnloadCache(budget).Item2;
        }

        public (Type, int) UnloadUnusedCache(int budget)
        {
            unloadCacheFilter = data => Time.unscaledTime - data.LastUsedTime > CacheCleaner.CACHE_EXPIRATION_TIME || IsNotReusedInHoldTime(data);

            return UnloadCache(budget);

            bool IsNotReusedInHoldTime(AssetBundleCacheData cacheData) =>
                cacheData.ReusedCount == 0 && Time.unscaledTime - cacheData.LastUsedTime > CacheCleaner.CACHE_MINIMAL_HOLD_TIME;
        }

        private (Type, int) UnloadCache(int budget)
        {
            cacheKeysToUnload.Clear();

            foreach (KeyValuePair<GetAssetBundleIntention, AssetBundleCacheData> pair in cache)
            {
                if (!frameBudget.TrySpendBudget() || cacheKeysToUnload.Count >= budget) break;

                if (unloadCacheFilter.Invoke(pair.Value))
                {
                    cacheKeysToUnload.Add(pair.Key);
                    pair.Value.Data.Dispose();
                }
            }

            foreach (GetAssetBundleIntention key in cacheKeysToUnload)
                cache.Remove(key);

            GameStats.ABCacheSizeCounter.Value = cache.Count;
            GameStats.ABCacheChangeCalls.Sample(cacheKeysToUnload.Count);

            return (typeof(AssetBundleData), cacheKeysToUnload.Count);
        }

        private class AssetBundleCacheData
        {
            public readonly AssetBundleData Data;

            public int ReusedCount;
            public float LastUsedTime;

            public AssetBundleCacheData(AssetBundleData assetData)
            {
                Data = assetData;
                LastUsedTime = Time.unscaledTime;
            }
        }
    }
}
