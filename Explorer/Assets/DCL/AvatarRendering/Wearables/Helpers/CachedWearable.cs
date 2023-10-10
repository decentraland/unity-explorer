using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     We need to store the original asset to be able to release it later
    /// </summary>
    public struct CachedWearable
    {
        public readonly WearableAsset OriginalAsset;
        public WearableAssetInstance Instance;

        public CachedWearable(WearableAsset originalAsset, WearableAssetInstance instance)
        {
            OriginalAsset = originalAsset;
            Instance = instance;
        }

        public static implicit operator GameObject(CachedWearable cachedWearable) =>
            cachedWearable.Instance.GameObject;
    }
}
