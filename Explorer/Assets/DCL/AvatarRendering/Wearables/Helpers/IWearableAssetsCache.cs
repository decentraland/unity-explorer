using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IWearableAssetsCache
    {
        int WearablesAssetsCount { get; }

        bool TryGet(WearableAsset asset, out CachedWearable instance);

        void Release(CachedWearable cachedWearable);

        void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
