using Cysharp.Threading.Tasks;
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
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;
        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(256, this);
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

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }
    }
}
