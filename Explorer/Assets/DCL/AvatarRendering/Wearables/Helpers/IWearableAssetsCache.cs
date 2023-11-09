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
            ///     Indicates that the asset was not returned to the pool because the pool is full
            ///     so the asset should be destroyed.
            ///     It is the responsibility of the caller to destroy the asset
            /// </summary>
            CapacityExceeded,

            /// <summary>
            ///     Indicates the the environment is being disposed so no actions related to the asset should be performed
            /// </summary>
            EnvironmentIsDisposing,
        }

        List<CachedWearable> AllCachedWearables { get; }

        bool TryGet(WearableAsset asset, out CachedWearable instance);

        ReleaseResult TryRelease(CachedWearable cachedWearable);

        bool TryUnloadCacheKey(WearableAsset asset);

        void UnloadCachedWearables();
    }
}
