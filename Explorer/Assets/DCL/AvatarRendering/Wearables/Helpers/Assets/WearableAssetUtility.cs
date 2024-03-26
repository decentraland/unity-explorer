using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableAssetUtility
    {
        public static void ReleaseAssets(this IWearableAssetsCache cache, IList<CachedWearable> instantiatedWearables)
        {
            foreach (CachedWearable cachedWearable in instantiatedWearables)
                cache.Release(cachedWearable);

            instantiatedWearables.Clear();
        }

        public static CachedWearable InstantiateWearable(this IWearableAssetsCache wearableAssetsCache, WearableRegularAsset originalAsset, Transform parent)
        {
            if (wearableAssetsCache.TryGet(originalAsset, out CachedWearable cachedWearable))
                cachedWearable.Instance.transform.SetParent(parent);
            else
                cachedWearable = new CachedWearable(originalAsset, Object.Instantiate(originalAsset.MainAsset, parent));

            cachedWearable.Instance.transform.ResetLocalTRS();
            cachedWearable.Instance.gameObject.layer = parent.gameObject.layer;

            using PoolExtensions.Scope<List<Transform>> children = cachedWearable.Instance.GetComponentsInChildrenIntoPooledList<Transform>(true);

            for (var index = 0; index < children.Value.Count; index++)
            {
                Transform child = children.Value[index];
                child.gameObject.layer = parent.gameObject.layer;
            }

            cachedWearable.Instance.gameObject.SetActive(true);
            return cachedWearable;
        }

        public static void SetAssetResult(this IWearable wearable, BodyShape bodyShape, int index, StreamableLoadingResult<WearableAssetBase> wearableResult)
        {
            ref var asset = ref wearable.WearableAssetResults[bodyShape];
            asset.Results[index] = wearableResult;
        }
    }
}
