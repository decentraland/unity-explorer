using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODCache
    {
        LODCacheInfo Get(in string key, Transform lodCacheParent, int lodLevels);

        void Release(in string key, LODCacheInfo asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
