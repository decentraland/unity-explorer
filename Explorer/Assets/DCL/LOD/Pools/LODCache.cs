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
        private readonly GameObjectPool<LODGroup> lodsGroupPool;
        private readonly Transform lodContainer;

        public LODCache()
        {
            lodContainer = new GameObject("POOL_CONTAINER_LODS").transform;
            lodsGroupPool = new GameObjectPool<LODGroup>(lodContainer.transform, LODGroupPoolUtils.CreateLODGroup, onRelease: LODGroupPoolUtils.ReleaseLODGroup);
            LODGroupPoolUtils.PrewarmLODGroupPool(lodsGroupPool);
            lodCache = new Dictionary<string, LODCacheInfo>();
        }

        public LODCacheInfo Get(in string key, int lodLevels)
        {
            //If in cache, return
            if (lodCache.Remove(key, out var asset))
            {
                asset.LodGroup.gameObject.SetActive(true);
                return asset;
            }

            //If not in cache, create new
            return new LODCacheInfo
            {
                LodGroup = InitializeLODGroup(key, lodContainer), LODAssets = new LODAsset[lodLevels]
            };
        }

        private LODGroup InitializeLODGroup(string sceneID, Transform lodCacheParent)
        {
            var newLODGroup = lodsGroupPool.Get();
            newLODGroup.name = $"LODGroup_{sceneID}";
            newLODGroup.transform.SetParent(lodCacheParent);
            return newLODGroup;
        }

        public void Release(in string key, LODCacheInfo asset)
        {
            Assert.IsFalse(lodCache.ContainsKey(key)); // 1 to 1 - relation, if it is true then we have a problem in our logic

            //We add to cache only if some LODs are loaded
            if (asset.LODLoadedCount() > 0)
            {
                asset.LodGroup.gameObject.SetActive(false);
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
