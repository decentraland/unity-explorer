using System;
using System.Collections.Generic;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Diagnostics;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Export
{
    public class WearableMeshCollector : IDisposable
    {
        private readonly List<GameObject> instantiatedObjects = new();

        /// <summary>
        /// Collects mesh data from wearables by checking which meshes are enabled in the scene instance,
        /// then getting the original SkinnedMeshRenderer data from the MainAsset using hierarchy path matching.
        /// </summary>
        public List<CollectedMeshData> CollectFromWearables(IReadOnlyList<CachedAttachment> instantiatedWearables)
        {
            var collectedMeshes = new List<CollectedMeshData>();

            foreach (var wearable in instantiatedWearables)
            {
                if (wearable.Instance == null || wearable.OriginalAsset?.MainAsset == null)
                    continue;

                var enabledPaths = GetEnabledMeshHierarchyPaths(wearable.Instance);
                
                if (enabledPaths.Count == 0)
                {
                    ReportHub.Log(ReportCategory.AVATAR_EXPORT, $"No enabled meshes found in wearable instance: {wearable.Instance.name}");
                    continue;
                }

                // Instantiate the original asset to get original SkinnedMeshRenderers
                var originalInstance = Object.Instantiate(wearable.OriginalAsset.MainAsset);
                originalInstance.name = wearable.OriginalAsset.MainAsset.name;
                instantiatedObjects.Add(originalInstance);

                // For each enabled path, find the corresponding renderer in original and collect it
                CollectMeshesAtPaths(originalInstance, enabledPaths, collectedMeshes);
            }

            return collectedMeshes;
        }

        /// <summary>
        /// Gets hierarchy paths (using sibling indices) of all enabled mesh renderers in the scene instance.
        /// Path format: "0/2/1" where each number is the sibling index at that level.
        /// </summary>
        private List<string> GetEnabledMeshHierarchyPaths(GameObject instance)
        {
            var enabledPaths = new List<string>();
            Transform root = instance.transform;

            var skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
            {
                if (smr.enabled && smr.gameObject.activeInHierarchy && smr.sharedMesh != null)
                {
                    string path = GetHierarchyPath(smr.transform, root);
                    enabledPaths.Add(path);
                }
            }

            var meshRenderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in meshRenderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) 
                    continue;
                
                var filter = renderer.GetComponent<MeshFilter>();
                
                if (filter == null || filter.sharedMesh == null) 
                    continue;
                
                string path = GetHierarchyPath(renderer.transform, root);
                enabledPaths.Add(path);
            }

            return enabledPaths;
        }

        /// <summary>
        /// Gets the hierarchy path from root to target using sibling indices.
        /// Returns path like "0/2/1" where each number is the sibling index at that level.
        /// </summary>
        private string GetHierarchyPath(Transform target, Transform root)
        {
            var indices = new List<int>();
            var current = target;

            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indices.Reverse();
            return string.Join("/", indices);
        }

        /// <summary>
        /// Finds a transform in the hierarchy using a path of sibling indices.
        /// </summary>
        private Transform FindTransformAtPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var splitPaths = path.Split('/');
            var current = root;

            foreach (var part in splitPaths)
            {
                if (!int.TryParse(part, out int index))
                {
                    ReportHub.LogError(ReportCategory.AVATAR_EXPORT, $"Invalid path part: {part}");
                    return null;
                }

                if (index < 0 || index >= current.childCount)
                {
                    ReportHub.LogError(ReportCategory.AVATAR_EXPORT, $"Child index {index} out of range at {current.name} (childCount: {current.childCount}");
                    return null;
                }

                current = current.GetChild(index);
            }

            return current;
        }

        /// <summary>
        /// Collects mesh data from original asset at the specified hierarchy paths.
        /// </summary>
        private void CollectMeshesAtPaths(GameObject originalInstance, List<string> paths, List<CollectedMeshData> collectedMeshes)
        {
            Transform root = originalInstance.transform;

            foreach (var path in paths)
            {
                Transform target = FindTransformAtPath(root, path);
                
                if (target == null)
                {
                    ReportHub.LogWarning(ReportCategory.AVATAR_EXPORT,$"Could not find transform at path: {path} in original");
                    continue;
                }
                
                var skinnedMeshRenderer = target.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    var meshData = CollectSkinnedMeshData(skinnedMeshRenderer);
                    if (meshData != null)
                        collectedMeshes.Add(meshData);
                    
                    continue;
                }

                var meshRenderer = target.GetComponent<MeshRenderer>();
                var mf = target.GetComponent<MeshFilter>();
                if (meshRenderer != null && mf != null && mf.sharedMesh != null)
                {
                    var meshData = CollectStaticMeshData(meshRenderer, mf);
                    if (meshData != null)
                        collectedMeshes.Add(meshData);
                    
                    continue;
                }

                ReportHub.LogWarning(ReportCategory.AVATAR_EXPORT,$"No valid renderer found at path: {path} ({target.name})");
            }
        }

        private CollectedMeshData CollectSkinnedMeshData(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var mesh = skinnedMeshRenderer.sharedMesh;
            var bones = skinnedMeshRenderer.bones;

            var boneNames = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                boneNames[i] = bones[i] != null ? bones[i].name : null;
            }

            float[] blendShapeWeights = null;
            if (mesh.blendShapeCount > 0)
            {
                blendShapeWeights = new float[mesh.blendShapeCount];
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    blendShapeWeights[i] = skinnedMeshRenderer.GetBlendShapeWeight(i);
                }
            }

            return new CollectedMeshData
            {
                Name = skinnedMeshRenderer.name,
                SharedMesh = mesh,
                Materials = skinnedMeshRenderer.sharedMaterials,
                IsSkinnedMesh = true,
                SourceBones = bones,
                SourceBoneNames = boneNames,
                RootBoneName = skinnedMeshRenderer.rootBone != null ? skinnedMeshRenderer.rootBone.name : null,
                BlendShapeWeights = blendShapeWeights
            };
        }

        private CollectedMeshData CollectStaticMeshData(MeshRenderer meshRenderer, MeshFilter meshFilter)
        {
            string parentPath = BuildParentPath(meshRenderer.transform);

            return new CollectedMeshData
            {
                Name = meshRenderer.name,
                SharedMesh = meshFilter.sharedMesh,
                Materials = meshRenderer.sharedMaterials,
                IsSkinnedMesh = false,
                OriginalParentPath = parentPath
            };
        }

        private string BuildParentPath(Transform t)
        {
            var parts = new List<string>();
            var current = t.parent;

            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
        
        public void Dispose()
        {
            foreach (var obj in instantiatedObjects)
            {
                if (obj != null)
                    UnityObjectUtils.SafeDestroy(obj);
            }
            instantiatedObjects.Clear();
        }
    }
}
