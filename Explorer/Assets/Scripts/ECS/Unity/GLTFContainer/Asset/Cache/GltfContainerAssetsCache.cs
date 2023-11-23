using Cysharp.Threading.Tasks;
using DCL.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
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
        private readonly Transform parentContainer;

        private readonly Dictionary<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> cache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<GltfContainerAsset>> IrrecoverableFailures { get; }

        private bool isDisposed { get; set; }

        public GltfContainerAssetsCache()
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{nameof(GltfContainerAsset)}").transform;
            parentContainer.gameObject.SetActive(false);

            cache = new Dictionary<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)>(this);

            OngoingRequests = new FakeDictionaryCache<UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>>();
            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<GltfContainerAsset>>.Get();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<AssetBundleData>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<AssetBundleData>>);
            isDisposed = true;
        }

        public bool TryGet(in string key, out GltfContainerAsset asset)
        {
            if (cache.TryGetValue(key, out (uint LastUsedFrame, List<GltfContainerAsset> Assets) value) && value.Assets.Count > 0)
            {
                // Remove from the tail of the list
                asset = value.Assets[^1];
                value.Assets.RemoveAt(value.Assets.Count - 1);
                value.LastUsedFrame = (uint)Time.frameCount;

                ProfilingCounters.GltfInCacheAmount.Value--;
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
            if (!cache.TryGetValue(key, out (uint LastUsedFrame, List<GltfContainerAsset> Assets) value))
            {
                value.Assets = new List<GltfContainerAsset>();
                cache[key] = value;
            }

            value.LastUsedFrame = (uint)Time.frameCount;
            value.Assets.Add(asset);

            ProfilingCounters.GltfInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer);
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            using (ListPool<KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)>>.Get(out List<KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)>> sortedCache))
            {
                PrepareListSortedByLastUsage(sortedCache);
                int totalUnloadedAssets = UnloadGltfAssets(frameTimeBudgetProvider, sortedCache);

                ProfilingCounters.GltfInCacheAmount.Value -= totalUnloadedAssets;
            }

            return;

            void PrepareListSortedByLastUsage(List<KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)>> sortedCache)
            {
                foreach (KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> item in cache)
                    sortedCache.Add(item);

                sortedCache.Sort(CompareByLastUsedFrame);
            }
        }

        private int UnloadGltfAssets(IConcurrentBudgetProvider frameTimeBudgetProvider, List<KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)>> sortedCache)
        {
            var totalUnloadedAssets = 0;

            foreach (KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> pair in sortedCache)
            {
                if (!frameTimeBudgetProvider.TrySpendBudget()) break;

                int disposedGltfAssets = DisposeGltfAssetsInSortedList(pair);
                ClearCache(pair, disposedGltfAssets);

                totalUnloadedAssets += disposedGltfAssets;
            }

            return totalUnloadedAssets;

            int DisposeGltfAssetsInSortedList(KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> pair)
            {
                var i = 0;

                for (; i < pair.Value.Assets.Count; i++)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;

                    pair.Value.Assets[i].Dispose();
                }

                return i;
            }

            void ClearCache(KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> pair, int disposeGltfAssets)
            {
                cache[pair.Key].Assets.RemoveRange(0, disposeGltfAssets);

                if (cache[pair.Key].Assets.Count == 0)
                    cache.Remove(pair.Key);
            }
        }

        bool IEqualityComparer<string>.Equals(string x, string y) =>
            string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        int IEqualityComparer<string>.GetHashCode(string obj) =>
            obj.GetHashCode(StringComparison.OrdinalIgnoreCase);

        private static int CompareByLastUsedFrame(KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> pair1, KeyValuePair<string, (uint LastUsedFrame, List<GltfContainerAsset> Assets)> pair2) =>
            pair1.Value.LastUsedFrame.CompareTo(pair2.Value.LastUsedFrame);
    }
}
