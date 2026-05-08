using DCL.AvatarRendering.Loading.DTO;
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
        // Mirrors ComputeShaderConstants.BASE_BONE_COUNT (DCL.AvatarRendering.AvatarShape asmdef).
        // Duplicated here because Loading cannot reference AvatarShape (cyclic asmdef dep).
        private const int AVATAR_SKELETON_BONE_COUNT = 62;

        public static void ReleaseAssets(this IAttachmentsAssetsCache cache, IList<CachedAttachment> instantiatedWearables)
        {
            foreach (CachedAttachment cachedWearable in instantiatedWearables)
                cache.Release(cachedWearable);

            instantiatedWearables.Clear();
        }

        public static CachedAttachment InstantiateWearable(this IAttachmentsAssetsCache attachmentsAssetsCache,
            AttachmentRegularAsset originalAsset,
            Transform parent,
            bool outlineCompatible,
            IReadOnlyDictionary<string, SpringBoneParamsDto>? springBonesParams = null)
        {
            CachedAttachment wearable = InstantiateOrGetCached(attachmentsAssetsCache, originalAsset, parent, outlineCompatible, springBonesParams);

            ProcessWearableChildren(parent, wearable);

            wearable.Instance.transform.ResetLocalTRS();
            wearable.Instance.gameObject.SetActive(true);

            return wearable;
        }

        private static CachedAttachment InstantiateOrGetCached(IAttachmentsAssetsCache attachmentsAssetsCache,
            AttachmentRegularAsset originalAsset,
            Transform parent,
            bool outlineCompatible,
            IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
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
            foreach (var meshRenderer in meshRenderers.Value) Object.DestroyImmediate(meshRenderer.gameObject);

            // Remove unused bone GameObjects from the wearable hierarchies, preserving spring bone transforms
            RemoveBonesGameObjects(instantiatedWearable.transform, springBoneParams);

            var springBones = BuildSpringBoneData(instantiatedWearable, springBoneParams);
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

        private static void RemoveBonesGameObjects(Transform wearableRoot, IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
        {
            if (wearableRoot.GetComponentInChildren<Renderer>(true) == null)
                return;

            for (int i = wearableRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wearableRoot.GetChild(i);

                if (!HasRendererInHierarchy(child) && !HasSpringBoneInHierarchy(child, springBoneParams))
                    Object.Destroy(child.gameObject);
            }
        }

        private static bool HasRendererInHierarchy(Transform transform) =>
            transform.GetComponentInChildren<Renderer>(true) != null;

        private static bool HasSpringBoneInHierarchy(Transform transform, IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
        {
            if (springBoneParams == null || springBoneParams.Count == 0)
                return false;

            if (springBoneParams.ContainsKey(transform.name))
                return true;

            for (int i = 0; i < transform.childCount; i++)
                if (HasSpringBoneInHierarchy(transform.GetChild(i), springBoneParams))
                    return true;

            return false;
        }

        private static SpringBoneData[] BuildSpringBoneData(GameObject wearable, IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
        {
            if (springBoneParams == null || springBoneParams.Count == 0)
                return Array.Empty<SpringBoneData>();

            var skeleton = wearable.GetComponentInChildren<SkinnedMeshRenderer>();
            Transform[] bones = skeleton.bones;

            using var resultScope = ListPool<SpringBoneData>.Get(out var result);
            using var boneIndexLookupScope = DictionaryPool<Transform, int>.Get(out var boneIndexLookup);

            for (int i = 0; i < bones.Length; i++)
                boneIndexLookup.Add(bones[i], i);

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];

                // Bone explicitly configured in the payload (root or follower)
                if (springBoneParams.TryGetValue(bone.name, out SpringBoneParamsDto cfg))
                {
                    result.Add(new SpringBoneData(bone, cfg.isRoot,
                        boneIndexLookup[bone.parent],
                        cfg.stiffness, cfg.drag, cfg.gravityDir, cfg.gravityPower,
                        bone.localRotation));
                    continue;
                }

                // Untagged extra bone (beyond the base avatar skeleton): inherit from
                // the nearest spring root ancestor reachable through bone parents.
                if (i < AVATAR_SKELETON_BONE_COUNT) continue;

                SpringBoneParamsDto? inherited = null;

                for (Transform a = bone.parent; a != null && boneIndexLookup.ContainsKey(a); a = a.parent)
                {
                    if (springBoneParams.TryGetValue(a.name, out SpringBoneParamsDto ancestorCfg) && ancestorCfg.isRoot)
                    {
                        inherited = ancestorCfg;
                        break;
                    }
                }

                if (inherited == null) continue;

                result.Add(new SpringBoneData(bone, isRoot: false,
                    boneIndexLookup[bone.parent],
                    inherited.stiffness, inherited.drag, inherited.gravityDir, inherited.gravityPower,
                    bone.localRotation));
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<SpringBoneData>();
        }
    }
}
