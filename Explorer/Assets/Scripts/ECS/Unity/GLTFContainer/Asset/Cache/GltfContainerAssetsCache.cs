using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace ECS.Unity.GLTFContainer.Asset.Cache
{
    /// <summary>
    ///     Individual pool for each GltfContainer source. LRU cache
    ///     <para>Gltf Containers can't be reused</para>
    /// </summary>
    public class GltfContainerAssetsCache : IGltfContainerAssetsCache, IEqualityComparer<string>
    {
        internal readonly Dictionary<string, List<GltfContainerAsset>> cache;
        private readonly Transform parentContainer;
        private readonly SimplePriorityQueue<string, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<GltfContainerAsset>> IrrecoverableFailures { get; }

        private bool isDisposed { get; set; }

        public GltfContainerAssetsCache()
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{nameof(GltfContainerAsset)}").transform;
            parentContainer.gameObject.SetActive(false);

            cache = new Dictionary<string, List<GltfContainerAsset>>(this);
            OngoingRequests = new FakeDictionaryCache<UniTaskCompletionSource<StreamableLoadingResult<GltfContainerAsset>?>>();
            IrrecoverableFailures = new Dictionary<string, StreamableLoadingResult<GltfContainerAsset>>();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            IrrecoverableFailures.Clear();
            isDisposed = true;
        }

        public bool TryGet(in string key, out GltfContainerAsset? asset)
        {
            if (cache.TryGetValue(key, out List<GltfContainerAsset> assets) && assets.Count > 0)
            {
                // Remove from the tail of the list
                asset = assets[^1];
                assets.RemoveAt(assets.Count - 1);
                unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);

                ProfilingCounters.GltfInCacheAmount.Value--;
                return true;
            }

            asset = default(GltfContainerAsset);
            return false;
        }

        /// <summary>
        ///     Return to the pool
        /// </summary>
        public void Dereference(in string key, GltfContainerAsset asset)
        {
            if (!cache.TryGetValue(key, out List<GltfContainerAsset> assets))
            {
                assets = new List<GltfContainerAsset>();
                cache[key] = assets;
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);
            }

            assets.Add(asset);
            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);

            ProfilingCounters.GltfInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer, false);
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudget.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out string key) && cache.TryGetValue(key, out List<GltfContainerAsset> assets))
            {
                unloadedAmount += assets.Count;

                foreach (GltfContainerAsset asset in assets)
                    asset.Dispose();

                assets.Clear();
                cache.Remove(key);
            }

            ProfilingCounters.GltfInCacheAmount.Value -= unloadedAmount;
        }

        bool IEqualityComparer<string>.Equals(string x, string y) =>
            string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        int IEqualityComparer<string>.GetHashCode(string obj) =>
            obj.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
