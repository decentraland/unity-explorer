using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODCache
    {
        bool TryGet(in string key, out LODCacheInfo cacheInfo);

        void Release(in string key, LODCacheInfo asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
        void UnloadImmediate();
    }
}
