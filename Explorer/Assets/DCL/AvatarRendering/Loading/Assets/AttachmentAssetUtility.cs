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

                // Remove unused bone GameObjects from the wearable hierarchies as we don't need them anymore
                RemoveBonesGameObjects(instantiatedWearable.transform);

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

        private static void RemoveBonesGameObjects(Transform wearableRoot)
        {
            using PoolExtensions.Scope<List<Renderer>> pooledList =
                wearableRoot.gameObject.GetComponentsInChildrenIntoPooledList<Renderer>(true);

            if (pooledList.Value.Count == 0)
                return;

            // Re-parent all renderer GameObjects directly to the wearable root to reduce the hierarchy clutter
            foreach (Renderer renderer in pooledList.Value)
            {
                Transform transform = renderer.transform;

                if (transform != wearableRoot && transform.parent != wearableRoot)
                    transform.SetParent(wearableRoot, true);
            }

            for (int i = wearableRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wearableRoot.GetChild(i);

                if (!HasRendererInHierarchy(child))
                    Object.Destroy(child.gameObject);
            }
        }

        private static bool HasRendererInHierarchy(Transform transform)
        {
            if (transform.GetComponent<Renderer>() != null)
                return true;

            for (int i = 0; i < transform.childCount; i++)
            {
                if (HasRendererInHierarchy(transform.GetChild(i)))
                    return true;
            }

            return false;
        }
    }
}
