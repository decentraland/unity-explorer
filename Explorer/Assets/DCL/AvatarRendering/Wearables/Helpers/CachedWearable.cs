using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     We need to store the original asset to be able to release it later
    /// </summary>
    public readonly struct CachedWearable
    {
        internal readonly GameObject originalAsset;
        public readonly GameObject Instance;

        public CachedWearable(GameObject originalAsset, GameObject instance)
        {
            this.originalAsset = originalAsset;
            Instance = instance;
        }

        public static implicit operator GameObject(CachedWearable cachedWearable) =>
            cachedWearable.Instance;
    }
}
