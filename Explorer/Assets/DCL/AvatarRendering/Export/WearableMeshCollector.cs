using System.Collections.Generic;
using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Export
{
    public class WearableMeshCollector
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

                // Step 1: Get hierarchy paths of all enabled meshes in scene instance
                var enabledPaths = GetEnabledMeshHierarchyPaths(wearable.Instance);
                
                if (enabledPaths.Count == 0)
                {
                    Debug.Log("No enabled meshes found in wearable instance: " + wearable.Instance.name);
                    continue;
                }

                // Step 2: Instantiate the original asset to get original SkinnedMeshRenderers
                var originalInstance = Object.Instantiate(wearable.OriginalAsset.MainAsset);
                originalInstance.name = wearable.OriginalAsset.MainAsset.name;
                instantiatedObjects.Add(originalInstance);

                // Step 3: For each enabled path, find the corresponding renderer in original and collect it
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

            // Check SkinnedMeshRenderers
            var skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
            {
                if (smr.enabled && smr.gameObject.activeInHierarchy && smr.sharedMesh != null)
                {
                    string path = GetHierarchyPath(smr.transform, root);
                    enabledPaths.Add(path);
                    Debug.Log("Found enabled SMR at path: " + path + " (" + smr.gameObject.name + ")");
                }
            }

            // Check MeshRenderers (GPU skinned meshes in DCL)
            var meshRenderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                if (mr.enabled && mr.gameObject.activeInHierarchy)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        string path = GetHierarchyPath(mr.transform, root);
                        enabledPaths.Add(path);
                        Debug.Log("Found enabled MR at path: " + path + " (" + mr.gameObject.name + ")");
                    }
                }
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

            var parts = path.Split('/');
            var current = root;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int index))
                {
                    Debug.LogWarning("Invalid path part: " + part);
                    return null;
                }

                if (index < 0 || index >= current.childCount)
                {
                    Debug.LogWarning("Child index " + index + " out of range at " + current.name + " (childCount: " + current.childCount + ")");
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
                    Debug.LogWarning("Could not find transform at path: " + path + " in original");
                    continue;
                }

                // Try to get SkinnedMeshRenderer (preferred - has bone data)
                var smr = target.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                {
                    var meshData = CollectSkinnedMeshData(smr);
                    if (meshData != null)
                    {
                        collectedMeshes.Add(meshData);
                        Debug.Log("Collected SMR at path: " + path + " (" + target.name + ")");
                    }
                    continue;
                }

                // Fallback to MeshRenderer if no SkinnedMeshRenderer
                var mr = target.GetComponent<MeshRenderer>();
                var mf = target.GetComponent<MeshFilter>();
                if (mr != null && mf != null && mf.sharedMesh != null)
                {
                    var meshData = CollectStaticMeshData(mr, mf);
                    if (meshData != null)
                    {
                        collectedMeshes.Add(meshData);
                        Debug.Log("Collected MR at path: " + path + " (" + target.name + ")");
                    }
                    continue;
                }

                Debug.LogWarning("No valid renderer found at path: " + path + " (" + target.name + ")");
            }
        }

        private CollectedMeshData CollectSkinnedMeshData(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            var bones = smr.bones;

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
                    blendShapeWeights[i] = smr.GetBlendShapeWeight(i);
                }
            }

            return new CollectedMeshData
            {
                Name = smr.name,
                SharedMesh = mesh,
                Materials = smr.sharedMaterials,
                IsSkinnedMesh = true,
                SourceBones = bones,
                SourceBoneNames = boneNames,
                RootBoneName = smr.rootBone != null ? smr.rootBone.name : null,
                Bounds = smr.localBounds,
                BlendShapeWeights = blendShapeWeights,
                SourceRenderer = smr
            };
        }

        private CollectedMeshData CollectStaticMeshData(MeshRenderer mr, MeshFilter mf)
        {
            string parentPath = BuildParentPath(mr.transform);

            return new CollectedMeshData
            {
                Name = mr.name,
                SharedMesh = mf.sharedMesh,
                Materials = mr.sharedMaterials,
                IsSkinnedMesh = false,
                OriginalParentPath = parentPath,
                LocalPosition = mr.transform.localPosition,
                LocalRotation = mr.transform.localRotation,
                LocalScale = mr.transform.localScale,
                Bounds = mf.sharedMesh.bounds,
                SourceRenderer = mr
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

        public void Cleanup()
        {
            return;
            foreach (var obj in instantiatedObjects)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            instantiatedObjects.Clear();
        }
    }
}
