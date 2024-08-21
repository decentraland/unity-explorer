using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace DCL.AvatarRendering.Loading.Assets
{
    /// <summary>
    ///     <para>
    ///         This class is used to store instances of wearable assets
    ///     </para>
    ///     <para>
    ///         It keeps a limited reasonable number of unique assets
    ///     </para>
    /// </summary>
    public class AttachmentsAssetsCache : IAttachmentsAssetsCache, IDisposable
    {
        // string is hash here which is retrieved via IWearable.GetMainFileHash
        private readonly ListObjectPool<CachedAttachment> listPool;
        private readonly Transform parentContainer;
        private readonly SimplePriorityQueue<AttachmentAssetBase, long> unloadQueue = new ();

        public int AssetsCount => cache.Count;

        internal Dictionary<AttachmentAssetBase, List<CachedAttachment>> cache { get; }

        public AttachmentsAssetsCache(int initialCapacity)
        {
            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(AttachmentsAssetsCache)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            cache = new Dictionary<AttachmentAssetBase, List<CachedAttachment>>(initialCapacity);

            // instantiate a couple of lists to prevent runtime allocations
            listPool = new ListObjectPool<CachedAttachment>(defaultCapacity: initialCapacity);
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(parentContainer);
        }

        public bool TryGet(AttachmentAssetBase asset, out CachedAttachment instance)
        {
            if (cache.TryGetValue(asset, out List<CachedAttachment> list) && list!.Count > 0)
            {
                // Remove from the tail of the list
                instance = list[^1];
                list.RemoveAt(list.Count - 1);

                if (list.Count == 0)
                {
                    cache.Remove(asset);
                    unloadQueue.Remove(asset);
                }
                else
                    unloadQueue.TryUpdatePriority(asset, MultithreadingUtility.FrameCount);

                ProfilingCounters.CachedWearablesInCacheAmount.Value--;
                return true;
            }

            instance = default(CachedAttachment);
            return false;
        }

        public void Release(CachedAttachment cachedAttachment)
        {
            AttachmentAssetBase asset = cachedAttachment.OriginalAsset;

            if (!cache.TryGetValue(asset, out List<CachedAttachment> list))
            {
                cache[asset] = list = listPool.Get()!;
                unloadQueue.Enqueue(asset, MultithreadingUtility.FrameCount);
            }
            else
                unloadQueue.TryUpdatePriority(asset, MultithreadingUtility.FrameCount);

            list!.Add(cachedAttachment);

            ProfilingCounters.CachedWearablesInCacheAmount.Value++;

            // This logic should not be executed if the application is quitting
            if (!UnityObjectUtils.IsQuitting)
            {
                cachedAttachment.Instance.SetActive(false);

                foreach (Renderer renderer in cachedAttachment.Renderers)
                    renderer.enabled = true;

                cachedAttachment.Instance.transform.SetParent(parentContainer);
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudget.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out AttachmentAssetBase key) && cache.TryGetValue(key, out List<CachedAttachment> assets))
            {
                unloadedAmount += assets!.Count;

                foreach (CachedAttachment asset in assets)
                    asset.Dispose();

                assets.Clear();
                cache.Remove(key);
            }

            ProfilingCounters.CachedWearablesInCacheAmount.Value -= unloadedAmount;
        }
    }
}
