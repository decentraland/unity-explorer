using DCL.Optimization.PerformanceBudgeting;

namespace DCL.LOD
{
    public interface ILODAssetsPool
    {
        bool TryGet(in LODKey key, out LODAsset asset);

        void Release(in LODKey key, LODAsset asset);

        void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
