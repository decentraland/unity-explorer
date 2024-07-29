using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;
using DCL.LOD.Components;
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
        private readonly GameObjectPool<LODGroup> lodsGroupPool;

        public LODCache(GameObjectPool<LODGroup> lodsGroupPool)
        {
            this.lodsGroupPool = lodsGroupPool;
            lodCache = new Dictionary<string, LODCacheInfo>();
        }

        public bool TryGet(in string key, out LODCacheInfo asset)
        {
            if (lodCache.Remove(key, out asset))
            {
                asset.LodGroup.enabled = true;
                return true;
            }

            asset = default;
            return false;
        }

        public void Release(in string key, LODCacheInfo asset)
        {
            Assert.IsFalse(lodCache.ContainsKey(key)); // 1 to 1 - relation, if it is true then we have a problem in our logic

            //We add to cache only if some LODs are loaded
            if (SceneLODInfoUtils.CountLOD(asset.LoadedLODs) > 1
                || SceneLODInfoUtils.CountLOD(asset.FailedLODs) > 1)
            {
                asset.LodGroup.enabled = false;
                lodCache[key] = asset;
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);
            }
            else
            {
                //The info has not been initialized. It can be returned to pool
                lodsGroupPool.Release(asset.LodGroup);
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
                lodsGroupPool.Release(asset.LodGroup);
                asset.Dispose();
            }
        }
    }
}
