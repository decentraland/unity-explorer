using DCL.Diagnostics;
using DCL.Optimization.Pools;
using GLTFast;
using System;
using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.Blittables;
using UniGLTF.SpringBoneJobs.InputPorts;
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
            CachedAttachment wearable = InstantiateOrGetCached(attachmentsAssetsCache, originalAsset, parent, outlineCompatible);

            ProcessWearableChildren(parent, wearable);

            wearable.Instance.gameObject.layer = parent.gameObject.layer;
            wearable.Instance.transform.ResetLocalTRS();
            wearable.Instance.gameObject.SetActive(true);

            return wearable;
        }

        private static CachedAttachment InstantiateOrGetCached(IAttachmentsAssetsCache attachmentsAssetsCache, AttachmentRegularAsset originalAsset, Transform parent, bool outlineCompatible)
        {
            if (attachmentsAssetsCache.TryGet(originalAsset, out CachedAttachment cachedWearable))
            {
                cachedWearable.Instance.transform.SetParent(parent);
                return cachedWearable;
            }

            var instantiatedWearable = Object.Instantiate(originalAsset.MainAsset, parent);

            using PoolExtensions.Scope<List<MeshRenderer>> meshRenderers = instantiatedWearable.GetComponentsInChildrenIntoPooledList<MeshRenderer>(true);

            // A wearable cannot have a MeshRenderer, only SkinnedMeshRenderer.
            // We need to destroy it form the source wearable
            foreach (var T in meshRenderers.Value) Object.DestroyImmediate(T.gameObject);

            // Remove unused bone GameObjects from the wearable hierarchies, preserving spring bone transforms
            RemoveBonesGameObjects(instantiatedWearable.transform);

            var springBones = BuildSpringBoneData(instantiatedWearable);
            return new CachedAttachment(originalAsset, instantiatedWearable, outlineCompatible, springBones);
        }

        private static void ProcessWearableChildren(Transform parent, CachedAttachment wearable)
        {
            using var children = wearable.Instance.GetComponentsInChildrenIntoPooledList<Transform>(true);

            foreach (var child in children.Value)
            {
                child.gameObject.layer = parent.gameObject.layer;

                // Wearables shouldn't have animators or animations since it will break the skinning
                Object.Destroy(child.GetComponent<Animator>());
                Object.Destroy(child.GetComponent<Animation>());
            }
        }

        private static void RemoveBonesGameObjects(Transform wearableRoot)
        {
            using PoolExtensions.Scope<List<Renderer>> pooledList = wearableRoot.gameObject.GetComponentsInChildrenIntoPooledList<Renderer>(true);

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
            transform.GetComponentInChildren<SpringBoneJointComponent>();

        private static SpringBoneData[] BuildSpringBoneData(GameObject wearable)
        {
            using var resultScope = ListPool<SpringBoneData>.Get(out var result);

            var skeleton = wearable.GetComponentInChildren<SkinnedMeshRenderer>();

            // Map each bone in the rig to its index in the bones array
            using var boneIndexLookupScope = DictionaryPool<Transform, int>.Get(out var boneIndexLookup);
            for (int boneIndex = 0; boneIndex < skeleton.bones.Length; boneIndex++)
                boneIndexLookup.Add(skeleton.bones[boneIndex], boneIndex);

            foreach (var bone in skeleton.bones)
            {
                SpringBoneJointComponent jointAuthoring = bone.GetComponent<SpringBoneJointComponent>();
                if (!jointAuthoring) continue;

                result.Add(new SpringBoneData(bone,
                    jointAuthoring.IsRoot,
                    boneIndexLookup[bone.parent], // Store parent bone index to sync transforms later on
                    jointAuthoring.Stiffness,
                    jointAuthoring.Drag,
                    jointAuthoring.GravityDir,
                    jointAuthoring.GravityPower,
                    jointAuthoring.HitRadius,
                    bone.localRotation)); // Store initial rotation to reset reused transforms later on
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<SpringBoneData>();
        }
    }
}
