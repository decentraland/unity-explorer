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
        internal readonly Dictionary<LODKey, LODAsset> vacantInstances;

        private readonly SimplePriorityQueue<LODKey, long> unloadQueue = new ();

        public LODAssetsPool()
        {
            vacantInstances = new Dictionary<LODKey, LODAsset>();
        }

        public bool TryGet(in LODKey key, out LODAsset asset)
        {
            if (vacantInstances.Remove(key, out asset))
            {
                asset.EnableAsset();
                return true;
            }

            asset = default(LODAsset);
            return false;
        }

        public void Release(in LODKey key, LODAsset asset)
        {
            Assert.IsFalse(vacantInstances.ContainsKey(key)); // 1 to 1 - relation, if it is true then we have a problem in our logic

            vacantInstances[key] = asset;
            unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);
            asset.DisableAsset();
        }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            var unloadedAmount = 0;

            while (frameTimeBudgetProvider.TrySpendBudget()
                   && unloadedAmount < maxUnloadAmount && unloadQueue.Count > 0
                   && unloadQueue.TryDequeue(out LODKey key) && vacantInstances.Remove(key, out LODAsset asset))
            {
                unloadedAmount++;
                asset.Dispose();
            }
        }
    }
}
