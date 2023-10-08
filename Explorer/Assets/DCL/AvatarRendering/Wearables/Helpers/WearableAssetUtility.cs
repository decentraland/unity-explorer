using DCL.AvatarRendering.Wearables.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableAssetUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TryReleaseAsset(this IWearableAssetsCache.ReleaseResult releaseResult, GameObject asset)
        {
            if (releaseResult == IWearableAssetsCache.ReleaseResult.CapacityExceeded)
                UnityObjectUtils.SafeDestroy(asset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject GetOriginalAsset(this IWearable wearable, BodyShape bodyShape) =>
            wearable.AssetBundleData[bodyShape].Value.Asset.GameObject;

        public static void TryReleaseAssets(this IWearableAssetsCache cache, IList<CachedWearable> instantiatedWearables)
        {
            for (var i = 0; i < instantiatedWearables.Count; i++)
            {
                CachedWearable cachedWearable = instantiatedWearables[i];
                GameObject instantiatedWearable = cachedWearable.Instance;
                IWearableAssetsCache.ReleaseResult releaseResult = cache.TryRelease(cachedWearable);
                releaseResult.TryReleaseAsset(instantiatedWearable);
            }

            instantiatedWearables.Clear();
        }

        public static CachedWearable InstantiateWearable(this IWearableAssetsCache wearableAssetsCache, GameObject originalAsset, Transform parent)
        {
            if (!wearableAssetsCache.TryGet(originalAsset, out GameObject instantiatedWearable))
                instantiatedWearable = Object.Instantiate(originalAsset, parent);
            else
                instantiatedWearable.transform.SetParent(parent);

            instantiatedWearable.transform.ResetLocalTRS();
            return new CachedWearable(originalAsset, instantiatedWearable);
        }
    }
}
