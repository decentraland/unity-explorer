using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     An eternal runtime cache to prevent parallel loading of the same bundle
    /// </summary>
    public class AssetBundleCache : IStreamableCache<AssetBundleData, GetAssetBundleIntention>
    {
        private static readonly Comparison<(GetAssetBundleIntention intention, AssetBundleData abData)> COMPARE_BY_LAST_USED_FRAME_REVERSED =
            (pair1, pair2) => pair2.abData.LastUsedFrame.CompareTo(pair1.abData.LastUsedFrame);

        internal readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;
        private readonly List<(GetAssetBundleIntention intention, AssetBundleData abData)> listedCache = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(this);
            IrrecoverableFailures = new Dictionary<string, StreamableLoadingResult<AssetBundleData>>();
            OngoingRequests = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            IrrecoverableFailures.Clear();
            OngoingRequests.Clear();

            disposed = true;
        }

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            if (cache.TryAdd(key, asset))
                listedCache.Add((key, asset));

            ProfilingCounters.AssetBundlesInCache.Value = cache.Count;
        }

        public void AddReference(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            asset.AddReference();
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            listedCache.Sort(COMPARE_BY_LAST_USED_FRAME_REVERSED);

            for (int i = listedCache.Count - 1; frameTimeBudget.TrySpendBudget() && i >= 0 && maxUnloadAmount > 0; i--)
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

            ProfilingCounters.AssetBundlesInCache.Value = cache.Count;
        }

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);
    }
}
