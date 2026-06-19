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

            // Remove unused bone GameObjects from the wearable hierarchies, preserving any
            // Transform referenced by a SkinnedMeshRenderer.bones array (skinning would break otherwise).
            RemoveBonesGameObjects(instantiatedWearable.transform);

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

        private static void RemoveBonesGameObjects(Transform wearableRoot)
        {
            if (wearableRoot.GetComponentInChildren<Renderer>(true) == null)
                return;

            using var skinnedRenderersScope = wearableRoot.gameObject.GetComponentsInChildrenIntoPooledList<SkinnedMeshRenderer>(true);
            using var referencedBonesScope = HashSetPool<Transform>.Get(out var referencedBones);

            foreach (SkinnedMeshRenderer smr in skinnedRenderersScope.Value)
            {
                Transform[] bones = smr.bones;
                for (int b = 0; b < bones.Length; b++)
                    if (bones[b] != null) referencedBones.Add(bones[b]);
            }

            for (int i = wearableRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wearableRoot.GetChild(i);

                if (!HasRendererInHierarchy(child) && !HasReferencedBoneInHierarchy(child, referencedBones))
                    Object.Destroy(child.gameObject);
            }
        }

        private static bool HasRendererInHierarchy(Transform transform) =>
            transform.GetComponentInChildren<Renderer>(true) != null;

        private static bool HasReferencedBoneInHierarchy(Transform transform, HashSet<Transform> referencedBones)
        {
            if (referencedBones.Contains(transform))
                return true;

            for (int i = 0; i < transform.childCount; i++)
                if (HasReferencedBoneInHierarchy(transform.GetChild(i), referencedBones))
                    return true;

            return false;
        }

        private static SpringBoneData[] BuildSpringBoneData(GameObject wearable, IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
        {
            var skeleton = wearable.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skeleton == null) return Array.Empty<SpringBoneData>();
            Transform[] bones = skeleton.bones;
            if (bones.Length <= AVATAR_SKELETON_BONE_COUNT) return Array.Empty<SpringBoneData>();

            bool hasParams = springBoneParams != null && springBoneParams.Count > 0;

            using var resultScope = ListPool<SpringBoneData>.Get(out var result);
            using var boneIndexLookupScope = DictionaryPool<Transform, int>.Get(out var boneIndexLookup);

            for (int i = 0; i < bones.Length; i++)
                boneIndexLookup.Add(bones[i], i);

            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];

                if (i < AVATAR_SKELETON_BONE_COUNT && (!hasParams || !springBoneParams!.ContainsKey(bone.name)))
                    continue;

                if (!boneIndexLookup.TryGetValue(bone.parent, out int parentIdx)) continue;

                // isRoot is derived purely from hierarchy: a chain root is a bone whose parent is
                // in the base avatar skeleton. RegisterSprings indexes skeleton[parentIdx] against
                // the 62-bone base array, so honoring an authored isRoot for a deeper bone reads
                // out of range and wrecks the parent extra via SyncWearableParentToAvatar.
                bool isRoot = parentIdx < AVATAR_SKELETON_BONE_COUNT;

                SpringBoneParamsDto cfgToUse;

                if (hasParams && springBoneParams!.TryGetValue(bone.name, out SpringBoneParamsDto explicitCfg))
                    cfgToUse = explicitCfg;
                else
                {
                    // Untagged extra: inherit params from nearest configured ancestor, otherwise
                    // fall back to neutral defaults so the bone still gets a slot in the global
                    // matrix array and is driven by its skeleton ancestor.
                    cfgToUse = DEFAULT_EXTRA_BONE_PARAMS;

                    if (hasParams)
                    {
                        for (Transform a = bone.parent; a != null && boneIndexLookup.ContainsKey(a); a = a.parent)
                        {
                            if (springBoneParams!.TryGetValue(a.name, out SpringBoneParamsDto ancestorCfg))
                            {
                                cfgToUse = ancestorCfg;
                                break;
                            }
                        }
                    }
                }

                result.Add(new SpringBoneData(bone, isRoot,
                    parentIdx,
                    cfgToUse.stiffness, cfgToUse.drag, cfgToUse.gravityDir, cfgToUse.gravityPower,
                    bone.localRotation));
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<SpringBoneData>();
        }

        private static readonly SpringBoneParamsDto DEFAULT_EXTRA_BONE_PARAMS = new ()
        {
            stiffness = 0.5f,
            drag = 0.4f,
            gravityDir = Vector3.zero,
            gravityPower = 0f,
            hitRadius = 0f,
            isRoot = false,
        };
    }
}
