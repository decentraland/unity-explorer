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

            wearable.Instance.gameObject.layer = parent.gameObject.layer;
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
            foreach (var T in meshRenderers.Value) Object.DestroyImmediate(T.gameObject);

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
            using PoolExtensions.Scope<List<Renderer>> pooledList = wearableRoot.gameObject.GetComponentsInChildrenIntoPooledList<Renderer>(true);

            if (pooledList.Value.Count == 0)
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

        private static void CollectSpringBoneChain(
            Transform root,
            SpringBoneParamsDto rootConfig,
            Dictionary<Transform, int> boneIndexLookup,
            IReadOnlyDictionary<string, SpringBoneParamsDto> springBoneParams,
            List<SpringBoneData> result)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!boneIndexLookup.TryGetValue(child, out int boneIndex)) continue;
                if (boneIndex < AVATAR_SKELETON_BONE_COUNT) continue;

                // If the child has its own entry in the payload, it's an independent root — skip
                if (springBoneParams.ContainsKey(child.name)) continue;

                result.Add(new SpringBoneData(
                    child,
                    isRoot: false,
                    boneIndexLookup[child.parent],
                    rootConfig.stiffness,
                    rootConfig.drag,
                    rootConfig.gravityDir,
                    rootConfig.gravityPower,
                    child.localRotation));

                CollectSpringBoneChain(child, rootConfig, boneIndexLookup, springBoneParams, result);
            }
        }

        private static SpringBoneData[] BuildSpringBoneData(GameObject wearable, IReadOnlyDictionary<string, SpringBoneParamsDto>? springBoneParams)
        {
            if (springBoneParams == null || springBoneParams.Count == 0)
                return Array.Empty<SpringBoneData>();

            using var resultScope = ListPool<SpringBoneData>.Get(out var result);

            var skeleton = wearable.GetComponentInChildren<SkinnedMeshRenderer>();

            // Map each bone in the rig to its index in the bones array
            using var boneIndexLookupScope = DictionaryPool<Transform, int>.Get(out var boneIndexLookup);
            for (int boneIndex = 0; boneIndex < skeleton.bones.Length; boneIndex++)
                boneIndexLookup.Add(skeleton.bones[boneIndex], boneIndex);

            foreach (var bone in skeleton.bones)
            {
                if (!springBoneParams.TryGetValue(bone.name, out SpringBoneParamsDto paramsDto)) continue;

                result.Add(new SpringBoneData(bone,
                    paramsDto.isRoot,
                    boneIndexLookup[bone.parent],
                    paramsDto.stiffness,
                    paramsDto.drag,
                    paramsDto.gravityDir,
                    paramsDto.gravityPower,
                    bone.localRotation));

                // Collect untagged children that inherit this root's spring bone parameters
                if (paramsDto.isRoot)
                    CollectSpringBoneChain(bone, paramsDto, boneIndexLookup, springBoneParams, result);
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<SpringBoneData>();
        }
    }
}
