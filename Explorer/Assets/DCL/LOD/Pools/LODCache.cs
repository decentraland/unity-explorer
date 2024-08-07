using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.Pools;
using UnityEngine;
using UnityEngine.Assertions;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace DCL.LOD
{
    public class LODCache : ILODCache
    {
        internal readonly Dictionary<string, LODCacheInfo> lodCache;
        private readonly SimplePriorityQueue<string, long> unloadQueue = new ();
        internal readonly IComponentPool<LODGroup> lodsGroupsPool;

        public LODCache(IComponentPool<LODGroup> lodsGroupsPool)
        {
            this.lodsGroupsPool = lodsGroupsPool;
            lodCache = new Dictionary<string, LODCacheInfo>();
        }

        public bool TryGet(in string key, out LODCacheInfo cacheInfo)
        {
            if (lodCache.Remove(key, out cacheInfo))
            {
                cacheInfo.LodGroup.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        public void Release(in string key, LODCacheInfo asset)
        {
            //We add to cache only if some LODs are loaded
            if (asset.LODLoadedCount() > 0)
            {
                Assert.IsFalse(lodCache.ContainsKey(key)); // 1 to 1 - relation, if it is true then we have a problem in our logic

                asset.LodGroup.gameObject.SetActive(false);
                lodCache[key] = asset;
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);
            }
            else
            {
                //The info has not been initialized. It can be returned to pool
                lodsGroupsPool.Release(asset.LodGroup);
            }
        }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudgetProvider.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out string key) && lodCache.Remove(key, out var asset))
            {
                unloadedAmount++;
                lodsGroupsPool.Release(asset.LodGroup);
                asset.Dispose();
            }
        }
    }
}
