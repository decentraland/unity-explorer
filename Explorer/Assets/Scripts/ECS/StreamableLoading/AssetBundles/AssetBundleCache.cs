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
        private static readonly Comparison<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> compareByLastUsedFrameReversed =
            (pair1, pair2) => pair2.Value.LastUsedFrame.CompareTo(pair1.Value.LastUsedFrame);
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;
        private readonly List<KeyValuePair<GetAssetBundleIntention, AssetBundleData>> listedCache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(this);

            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Get();
            listedCache = ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>);
            ListPool<KeyValuePair<GetAssetBundleIntention, AssetBundleData>>.Release(listedCache);

            disposed = true;
        }

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
            listedCache.Add(new KeyValuePair<GetAssetBundleIntention, AssetBundleData>(key, asset));
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            listedCache.Sort(compareByLastUsedFrameReversed);

            for (int i = listedCache.Count - 1; i >= 0 && maxUnloadAmount > 0; i--)
            {
                (GetAssetBundleIntention key, AssetBundleData abData) = listedCache[i];
                if (!abData.CanBeDisposed()) continue;

                foreach (AssetBundleData child in abData.Dependencies)
                    child?.Dereference();

                abData.Dispose();
                cache.Remove(key);

                listedCache.RemoveAt(i);

                maxUnloadAmount--;
            }
        }

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);
    }
}
