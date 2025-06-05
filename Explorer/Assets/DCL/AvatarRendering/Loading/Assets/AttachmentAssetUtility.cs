﻿using DCL.Optimization.Pools;
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

                //A wearable cannot have a MeshRenderer, only SkinnedMeshRenderer.
                //We need to destroy it if thats the case
                foreach (MeshRenderer? mr in instantiatedWearable.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                    Object.DestroyImmediate(mr.gameObject);

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
                Object.Destroy(child.GetComponent<Animator>());
                Object.Destroy(child.GetComponent<Animation>());
            }

            cachedWearable.Instance.gameObject.SetActive(true);
            return cachedWearable;
        }
    }
}
