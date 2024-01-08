using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IWearableAssetsCache
    {
        int WearablesAssesCount { get; }

        bool TryGet(WearableAsset asset, out CachedWearable instance);

        void Release(CachedWearable cachedWearable);

        void Unload(IReleasablePerformanceBudget frameTimeBudget, int maxUnloadAmount);
    }
}
