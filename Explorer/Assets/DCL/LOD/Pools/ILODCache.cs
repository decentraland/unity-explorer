using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.LOD
{
    public interface ILODCache
    {
        bool TryGet(in string key, out LODCacheInfo asset);

        void Release(in string key, LODCacheInfo asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
