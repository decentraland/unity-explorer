using DCL.LOD.Components;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.LOD
{
    /// <summary>
    /// No-op ILODCache for WebGL where LOD streaming is disabled.
    /// </summary>
    public class NoOpLODCache : ILODCache
    {
        public bool TryGet(in string key, out LODCacheInfo cacheInfo)
        {
            cacheInfo = null!;
            return false;
        }

        public void Release(in string key, LODCacheInfo asset) { }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount) { }
    }
}
