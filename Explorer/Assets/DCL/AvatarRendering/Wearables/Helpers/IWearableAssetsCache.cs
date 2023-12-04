using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IWearableAssetsCache
    {
        List<CachedWearable> AllCachedWearables { get; }

        Dictionary<WearableAsset, List<CachedWearable>> Cache { get; }

        bool TryGet(WearableAsset asset, out CachedWearable instance);

        void Release(CachedWearable cachedWearable);

        void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount);
    }
}
