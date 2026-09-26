using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.LOD
{
    public interface IRoadAssetPool
    {
        bool Get(string key, out Transform roadAsset);

        void Release(string key, Transform asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);

        /// <summary>Fills every pool to its initial capacity so the first road placements do not instantiate on demand.</summary>
        void Prewarm();
    }
}
