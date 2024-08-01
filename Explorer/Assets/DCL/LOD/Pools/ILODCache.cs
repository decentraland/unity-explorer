using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODCache
    {
        void PrewarmLODGroupPool(int lodLevels, int lodgroupPoolPrewarmValue);
        
        LODCacheInfo Get(in string key, int lodLevels);

        void Release(in string key, LODCacheInfo asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
