using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.Pools;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableAssetUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WearableAsset GetOriginalAsset(this IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssetResults[bodyShape].Value.Asset;

        public static StreamableLoadingResult<WearableAsset> ToWearableAsset(this StreamableLoadingResult<AssetBundleData> result)
        {
            if (!result.Succeeded) return new StreamableLoadingResult<WearableAsset>(result.Exception);

            // in case of a texture we return a WearableAsset with texture
            if (result.Asset.GetMainAsset<Texture>() != null)
                return new StreamableLoadingResult<WearableAsset>(new WearableAsset(result.Asset.GetMainAsset<Texture>(), WearableAsset.RENDERER_INFO_POOL.Get(), result.Asset));

            if(result.Asset.GetMainAsset<GameObject>() != null)
            {
                // collect all renderers
                List<WearableAsset.RendererInfo> rendererInfos = WearableAsset.RENDERER_INFO_POOL.Get();
           
                using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = result.Asset.GetMainAsset<GameObject>().GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

                foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                    rendererInfos.Add(new WearableAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));

                return new StreamableLoadingResult<WearableAsset>(new WearableAsset(result.Asset.GetMainAsset<GameObject>(), rendererInfos, result.Asset));
            }
            
            //If we got here, there is an issue with the AB
            return new StreamableLoadingResult<WearableAsset>(new WearableAsset(null, WearableAsset.RENDERER_INFO_POOL.Get(), result.Asset));
        }

        public static void ReleaseAssets(this IWearableAssetsCache cache, IList<CachedWearable> instantiatedWearables)
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
                cachedWearable = new CachedWearable(originalAsset, Object.Instantiate(originalAsset.GetMainAsset<GameObject>(), parent));

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
    }
}
