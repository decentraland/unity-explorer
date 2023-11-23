using DCL.PerformanceBudgeting;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public interface IWearableAssetsCache
    {
        public enum ReleaseResult
        {
            /// <summary>
            ///     Indicates that the asset was successfully returned to the pool
            /// </summary>
            ReturnedToPool,

            /// <summary>
            ///     Indicates the the environment is being disposed so no actions related to the asset should be performed
            /// </summary>
            EnvironmentIsDisposing,
        }

        List<CachedWearable> AllCachedWearables { get; }
        Dictionary<WearableAsset, (uint LastUsedFrame, List<CachedWearable> Assets)> Cache { get; }

        bool TryGet(WearableAsset asset, out CachedWearable instance);

        ReleaseResult TryRelease(CachedWearable cachedWearable);

        void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider);
    }
}
