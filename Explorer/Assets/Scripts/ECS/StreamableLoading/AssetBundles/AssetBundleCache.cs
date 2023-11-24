using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
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

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(this);

            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>);

            disposed = true;
        }

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset) =>
            cache.Add(key, asset);

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            using (ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Get(out List<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> sortedCache))
            {
                PrepareListSortedByLastUsage(sortedCache);

                foreach ((GetAssetBundleIntention key, AssetBundleData abData) in sortedCache)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;
                    if (!abData.CanBeDisposed()) continue;

                    foreach (AssetBundleData child in abData.Dependencies)
                        child?.Dereference();

                    abData.Dispose();
                    cache.Remove(key);
                }
            }

            return;

            void PrepareListSortedByLastUsage(List<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> sortedCache)
            {
                foreach (KeyValuePair<GetAssetBundleIntention, AssetBundleData> item in cache)
                    sortedCache.Add(item);

                sortedCache.Sort(CompareByLastUsedFrame);
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
