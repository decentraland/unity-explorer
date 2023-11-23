using Cysharp.Threading.Tasks;
using DCL.PerformanceBudgeting;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : IStreamableCache<AssetBundleData, GetAssetBundleIntention>
    {
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;
        private readonly List<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> sortedCache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(this);

            sortedCache = ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Get();
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Release(sortedCache);
            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>);

            disposed = true;
        }

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
            sortedCache.Add(new KeyValuePair<GetAssetBundleIntention, AssetBundleData>(key, asset));
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            using (ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Get(out List<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> unloadedPairs))
            {
                sortedCache.Sort(CompareByLastUsedFrame);

                foreach (KeyValuePair<GetAssetBundleIntention, AssetBundleData> pair in sortedCache)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;
                    if (!pair.Value.CanBeDisposed()) continue;

                    foreach (AssetBundleData child in pair.Value.Dependencies)
                        child?.Dereference();

                    pair.Value.Dispose();

                    unloadedPairs.Add(pair);
                }

                foreach (KeyValuePair<GetAssetBundleIntention, AssetBundleData> pair in unloadedPairs)
                {
                    sortedCache.Remove(pair);
                    cache.Remove(pair.Key);
                }
            }
        }

        private static int CompareByLastUsedFrame(KeyValuePair<GetAssetBundleIntention, AssetBundleData> pair1, KeyValuePair<GetAssetBundleIntention, AssetBundleData> pair2) =>
            pair1.Value.LastUsedFrame.CompareTo(pair2.Value.LastUsedFrame);

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);
    }
}
