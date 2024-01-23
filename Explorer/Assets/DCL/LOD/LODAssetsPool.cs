using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace DCL.LOD
{
    public class LODAssetsPool : ILODAssetsPool
    {
        internal readonly Dictionary<string, LODAsset> vacantInstances;

        private readonly Transform parentContainer;
        private readonly SimplePriorityQueue<string, long> unloadQueue = new ();
        private IStreamableCache<LODAsset, string> streamableCacheImplementation;

        public LODAssetsPool()
        {
            parentContainer = new GameObject("POOL_CONTAINER_LodCache").transform;
            parentContainer.gameObject.SetActive(false);

            vacantInstances = new Dictionary<string, LODAsset>(StringComparer.CurrentCultureIgnoreCase);
        }

        public bool TryGet(in string key, out LODAsset asset)
        {
            if (key != null && vacantInstances.Remove(key, out asset))
            {
                ProfilingCounters.LODInstantiatedInCache.Value--;

                asset.Root.SetActive(true);
                asset.Root.transform.SetParent(null);
                return true;
            }

            asset = default(LODAsset);
            return false;
        }

        public void Release(in string key, LODAsset asset)
        {
            if (key == null)
                return;

            Assert.IsFalse(vacantInstances.ContainsKey(key)); // 1 to 1 - relation, if it is true then we have a problem in our logic

            vacantInstances[key] = asset;
            unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);

            ProfilingCounters.LODInstantiatedInCache.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            asset.Root.SetActive(false);
            asset.Root.transform.SetParent(parentContainer);
        }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudgetProvider.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out string key) && vacantInstances.TryGetValue(key, out LODAsset asset))
            {
                unloadedAmount++;
                asset.Dispose();
                vacantInstances.Remove(key);
            }

            ProfilingCounters.LODInstantiatedInCache.Value -= unloadedAmount;
        }
    }
}
