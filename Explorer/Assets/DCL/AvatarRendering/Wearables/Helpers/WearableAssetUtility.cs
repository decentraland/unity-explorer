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

            // in case of a texture just return the result
            if (result.Asset.GameObject == null)
                return new StreamableLoadingResult<WearableAsset>(new WearableAsset(null, WearableAsset.RENDERER_INFO_POOL.Get(), result.Asset));

            // collect all renderers
            List<WearableAsset.RendererInfo> rendererInfos = WearableAsset.RENDERER_INFO_POOL.Get();

            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> pooledList = result.Asset.GameObject.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in pooledList.Value)
                rendererInfos.Add(new WearableAsset.RendererInfo(skinnedMeshRenderer, skinnedMeshRenderer.sharedMaterial));

            return new StreamableLoadingResult<WearableAsset>(new WearableAsset(result.Asset.GameObject, rendererInfos, result.Asset));
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
                cachedWearable = new CachedWearable(originalAsset, Object.Instantiate(originalAsset.GameObject, parent));

            cachedWearable.Instance.transform.ResetLocalTRS();
            cachedWearable.Instance.gameObject.layer = parent.gameObject.layer;

            foreach (var child in cachedWearable.Instance.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = parent.gameObject.layer;
            }

            //TODO: Fran -> This code can be probably improved, check if we can get the list of children from somewhere else to avoid allocations.
            //We tried this, but it did not work correctly (not all wearables were correctly layered) so it might need some more work to get all children
            /*int renderersCount = cachedWearable.Renderers.Count;
            for (var index = 0; index < renderersCount; index++)
            {
                Renderer renderer = cachedWearable.Renderers[index];
                renderer.gameObject.layer = parent.gameObject.layer;
            }*/

            cachedWearable.Instance.gameObject.SetActive(true);
            return cachedWearable;
        }
    }
}
