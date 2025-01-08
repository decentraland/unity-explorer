using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.LOD
{
    public interface IRoadAssetPool
    {
        bool Get(string key, out Transform roadAsset);

        void Release(string key, Transform asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);

        void SwitchVisibility(bool isVisible);
    }
}
