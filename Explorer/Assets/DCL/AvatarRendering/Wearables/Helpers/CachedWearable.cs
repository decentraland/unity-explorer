using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     We need to store the original asset to be able to release it later
    /// </summary>
    public readonly struct CachedWearable
    {
        public readonly WearableAsset OriginalAsset;
        public readonly GameObject Instance;

        public CachedWearable(WearableAsset originalAsset, GameObject instance)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
        }

        public static implicit operator GameObject(CachedWearable cachedWearable) =>
            cachedWearable.Instance;
    }
}
