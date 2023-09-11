using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
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

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(256, this);
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Get();
        }

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            x.Hash == y.Hash;

        public int GetHashCode(GetAssetBundleIntention obj) =>
            obj.Hash.GetHashCode();



        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            cache.Add(key, asset);
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }

        public void Dispose()
        {
            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>>);
        }
    }
}
