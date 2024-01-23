using DCL.Optimization.PerformanceBudgeting;

namespace DCL.LOD
{
    public interface ILODAssetsPool
    {
        bool TryGet(in string key, out LODAsset asset);

        void Release(in string key, LODAsset asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
