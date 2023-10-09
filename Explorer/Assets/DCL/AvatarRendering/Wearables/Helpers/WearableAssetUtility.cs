using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;
using Utility.Pool;

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
        public static WearableAsset GetOriginalAsset(this IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssets[bodyShape].Value.Asset;

        public static StreamableLoadingResult<WearableAsset> ToWearableAsset(this StreamableLoadingResult<AssetBundleData> result)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<WearableAsset>(result.Exception);

            // in case of a texture just return the result
            if (result.Asset.GameObject == null)
                return new StreamableLoadingResult<WearableAsset>(new WearableAsset(null, WearableAsset.RENDERER_INFO_POOL.Get()));

            // collect all renderers
            List<WearableAsset.RendererInfo> rendererInfos = WearableAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = result.Asset.GameObject.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            for (var i = 0; i < pooledList.Value.Count; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = pooledList.Value[i];
                rendererInfos.Add(new WearableAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));
            }

            return new StreamableLoadingResult<WearableAsset>(new WearableAsset(result.Asset.GameObject, rendererInfos));
        }

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

        public static CachedWearable InstantiateWearable(this IWearableAssetsCache wearableAssetsCache, WearableAsset originalAsset, Transform parent)
        {
            if (!wearableAssetsCache.TryGet(originalAsset.GameObject, out GameObject instantiatedWearable))
                instantiatedWearable = Object.Instantiate(originalAsset.GameObject, parent);
            else
                instantiatedWearable.transform.SetParent(parent);

            instantiatedWearable.transform.ResetLocalTRS();
            instantiatedWearable.gameObject.SetActive(true);
            return new CachedWearable(originalAsset, instantiatedWearable);
        }
    }
}
