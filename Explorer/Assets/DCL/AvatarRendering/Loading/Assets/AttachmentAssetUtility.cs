using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

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

                // Remove unused bone GameObjects from the wearable hierarchies, preserving spring bone transforms
                RemoveBonesGameObjects(instantiatedWearable.transform);

                SpringBoneData[] springBones = CollectSpringBonesFromSMRs(instantiatedWearable);
                cachedWearable = new CachedAttachment(originalAsset, instantiatedWearable, outlineCompatible, springBones);
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

            for (int i = wearableRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wearableRoot.GetChild(i);

                if (!HasRendererInHierarchy(child) && !HasSpringBoneInHierarchy(child))
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

        private static bool HasSpringBoneInHierarchy(Transform transform)
        {
            if (transform.name.Contains("springbone", StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < transform.childCount; i++)
            {
                if (HasSpringBoneInHierarchy(transform.GetChild(i)))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Collects spring bone transforms from all SMRs in the wearable.
        ///     Each entry stores the transform and, for chain roots, the parent skeleton bone name.
        ///     A chain root is a spring bone whose parent is NOT another spring bone (e.g. parented to Neck).
        ///     Only chain roots need reparenting during avatar assembly — chain children follow automatically.
        /// </summary>
        private static SpringBoneData[] CollectSpringBonesFromSMRs(GameObject wearableRoot)
        {
            using PoolExtensions.Scope<List<SkinnedMeshRenderer>> smrs =
                wearableRoot.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>(true);

            if (smrs.Value.Count == 0)
                return Array.Empty<SpringBoneData>();

            HashSet<Transform> seen = HashSetPool<Transform>.Get();
            List<SpringBoneData> result = ListPool<SpringBoneData>.Get();

            for (var i = 0; i < smrs.Value.Count; i++)
            {
                Transform[] bones = smrs.Value[i].bones;

                for (var j = 0; j < bones.Length; j++)
                {
                    Transform t = bones[j];

                    if (t != null
                        && t.name.Contains("springbone", StringComparison.OrdinalIgnoreCase)
                        && seen.Add(t))
                    {
                        Transform parent = t.parent;
                        bool isChainRoot = parent != null && !parent.name.Contains("springbone", StringComparison.OrdinalIgnoreCase);
                        result.Add(new SpringBoneData(t, isChainRoot ? parent.name : null));
                    }
                }
            }

            SpringBoneData[] output = result.Count > 0 ? result.ToArray() : Array.Empty<SpringBoneData>();

            HashSetPool<Transform>.Release(seen);
            ListPool<SpringBoneData>.Release(result);

            return output;
        }
    }
}
