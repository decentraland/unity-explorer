using Cysharp.Threading.Tasks;
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
        public static bool destroyCache;
        private readonly Dictionary<GetAssetBundleIntention, AssetBundleData> cache;
        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AssetBundleData>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AssetBundleData>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public AssetBundleCache()
        {
            cache = new Dictionary<GetAssetBundleIntention, AssetBundleData>(256, this);
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

        public bool Equals(GetAssetBundleIntention x, GetAssetBundleIntention y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Hash, y.Hash);

        public int GetHashCode(GetAssetBundleIntention obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Hash);

        public bool TryGet(in GetAssetBundleIntention key, out AssetBundleData asset)
        {
            if (destroyCache)
            {
                Dispose();

                foreach (AssetBundleData assetBundleData in cache.Values)
                    assetBundleData.AssetBundle.Unload(false);

                cache.Clear();
                asset = null;
                return false;
            }

            if (cache.TryGetValue(key, out asset))
            {
                UnityEngine.Debug.Log($"VV:: Try get = {cache.Count}", asset.GameObject);
                return true;
            }

            return false;
        }

        public void Add(in GetAssetBundleIntention key, AssetBundleData asset)
        {
            if (destroyCache)
            {
                Dispose();

                foreach (AssetBundleData assetBundleData in cache.Values)
                    assetBundleData.AssetBundle.Unload(false);

                cache.Clear();
                return;
            }
            else
            {
                UnityEngine.Debug.Log($"VV:: Add = {cache.Count}", asset.GameObject);
                cache.Add(key, asset);
            }
        }

        public void Dereference(in GetAssetBundleIntention key, AssetBundleData asset) { }
    }
}
