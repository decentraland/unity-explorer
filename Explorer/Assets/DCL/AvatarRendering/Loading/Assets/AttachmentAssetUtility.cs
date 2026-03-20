using DCL.Optimization.Pools;
using GLTFast;
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

        private static bool HasSpringBoneInHierarchy(Transform transform) =>
            transform.GetComponentInChildren<SpringBoneJointComponent>() != null;

        /// <summary>
        ///     Collects spring bone transforms from all SMRs in the wearable by checking for
        ///     <see cref="SpringBoneJointComponent"/> on each bone. Iterates SMR bones to preserve
        ///     BoneWeight index order. Chain roots are identified by <see cref="SpringBoneJointComponent.IsRoot"/>.
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

                    SpringBoneJointComponent component;

                    if (t != null
                        && (component = t.GetComponent<SpringBoneJointComponent>()) != null
                        && seen.Add(t))
                    {
                        string parentName = component.IsRoot && t.parent != null ? t.parent.name : null;
                        result.Add(new SpringBoneData(t, parentName));
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
