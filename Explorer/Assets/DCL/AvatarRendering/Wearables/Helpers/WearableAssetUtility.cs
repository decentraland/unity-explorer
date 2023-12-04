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
        public static WearableAsset GetOriginalAsset(this IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssetResults[bodyShape]?.Asset;

        public static StreamableLoadingResult<WearableAsset> ToWearableAsset(this StreamableLoadingResult<AssetBundleData> result)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<WearableAsset>(result.Exception);

            // in case of a texture just return the result
            if (result.Asset.GameObject == null)
                return new StreamableLoadingResult<WearableAsset>(new WearableAsset(null, WearableAsset.RENDERER_INFO_POOL.Get(), result.Asset));

            // collect all renderers
            List<WearableAsset.RendererInfo> rendererInfos = WearableAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = result.Asset.GameObject.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            for (var i = 0; i < pooledList.Value.Count; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = pooledList.Value[i];
                rendererInfos.Add(new WearableAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));
            }

            return new StreamableLoadingResult<WearableAsset>(new WearableAsset(result.Asset.GameObject, rendererInfos, result.Asset));
        }

        public static void TryReleaseAssets(this IWearableAssetsCache cache, IList<CachedWearable> instantiatedWearables)
        {
            foreach (CachedWearable cachedWearable in instantiatedWearables)
                cache.Release(cachedWearable);

            instantiatedWearables.Clear();
        }

        public static CachedWearable InstantiateWearable(this IWearableAssetsCache wearableAssetsCache, WearableAsset originalAsset, Transform parent)
        {
            if (wearableAssetsCache.TryGet(originalAsset, out CachedWearable cachedWearable))
                cachedWearable.Instance.transform.SetParent(parent);
            else
            {
                cachedWearable = new CachedWearable(originalAsset, Object.Instantiate(originalAsset.GameObject, parent));
                wearableAssetsCache.AllCachedWearables.Add(cachedWearable);
            }

            cachedWearable.Instance.transform.ResetLocalTRS();
            cachedWearable.Instance.gameObject.SetActive(true);
            return cachedWearable;
        }
    }
}
