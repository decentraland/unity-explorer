using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IWearableAssetsCache
    {
        int WearablesAssesCount { get; }

        bool TryGet(WearableAssetBase asset, out CachedWearable instance);

        void Release(CachedWearable cachedWearable);

        void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount);
    }
}
