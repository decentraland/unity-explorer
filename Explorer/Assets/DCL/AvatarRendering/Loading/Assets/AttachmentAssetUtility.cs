using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.AvatarRendering.Loading.Assets
{
    public static class AttachmentAssetUtility
    {
        public static void ReleaseAssets(this IAttachmentsAssetsCache cache, IList<CachedAttachment> instantiatedWearables)
        {
            foreach (CachedAttachment cachedWearable in instantiatedWearables)
                cache.Release(cachedWearable);

            instantiatedWearables.Clear();
        }

        public static CachedAttachment InstantiateWearable(this IAttachmentsAssetsCache attachmentsAssetsCache, AttachmentRegularAsset originalAsset, Transform parent, bool outlineCompatible)
        {
            if (attachmentsAssetsCache.TryGet(originalAsset, out CachedAttachment cachedWearable))
                cachedWearable.Instance.transform.SetParent(parent);
            else
            {
                var instantiatedWearable = Object.Instantiate(originalAsset.MainAsset, parent);

                using PoolExtensions.Scope<List<MeshRenderer>> meshRenderers = instantiatedWearable.GetComponentsInChildrenIntoPooledList<MeshRenderer>(true);

                //A wearable cannot have a MeshRenderer, only SkinnedMeshRenderer.
                //We need to destroy it form the source wearable
                for (var i = 0; i < meshRenderers.Value.Count; i++)
                    Object.DestroyImmediate(meshRenderers.Value[i].gameObject);

                instantiatedWearable.name = originalAsset.GetInstanceName();
                cachedWearable = new CachedAttachment(originalAsset, instantiatedWearable, outlineCompatible);
            }

            cachedWearable.Instance.transform.ResetLocalTRS();
            cachedWearable.Instance.gameObject.layer = parent.gameObject.layer;

            using PoolExtensions.Scope<List<Transform>> children = cachedWearable.Instance.GetComponentsInChildrenIntoPooledList<Transform>(true);

            for (var index = 0; index < children.Value.Count; index++)
            {
                Transform child = children.Value[index];
                child.gameObject.layer = parent.gameObject.layer;

                //Wearables shouldnt have animators or animations since it will break the skinning
                UnityObjectUtils.SafeDestroy(child.GetComponent<Animator>());
                UnityObjectUtils.SafeDestroy(child.GetComponent<Animation>());
            }

            cachedWearable.Instance.gameObject.SetActive(true);
            return cachedWearable;
        }
    }
}
