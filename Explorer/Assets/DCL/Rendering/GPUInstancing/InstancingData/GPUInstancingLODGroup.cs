using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingLODGroup : MonoBehaviour, IEquatable<GPUInstancingLODGroup>
    {
        private const int MAX_LODS_LEVEL = 8;
        public Shader[] whitelistedShaders;

        [Header("REFERENCES")]
        public string Name;
        public LODGroup Reference;
        public Transform Transform;

        [Header("LOD GROUP DATA")]
        public float ObjectSize;
        public Bounds Bounds;
        public float[] LodsScreenSpaceSizes;

        [Space]
        public List<CombinedLodsRenderer> CombinedLodsRenderers;

        [Header("BUFFERS")]
        public List<PerInstanceBuffer> InstancesBuffer;
        public List<Matrix4x4> InstancesBufferDirect;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            if (GetComponentsInChildren<GPUInstancingLODGroup>().Length != 0)
                Debug.LogWarning($"{name} has nested GPU instancing candidates, that could lead to duplication of meshes!");

            CombinedLodsRenderers = new List<CombinedLodsRenderer>();

            LODGroup lodGroup = GetComponent<LODGroup>();

            if (lodGroup == null)
            {
                Debug.LogWarning("Selected GameObject does not have a LODGroup component.");
                return;
            }

            LOD[] lods = lodGroup.GetLODs();

            if (lods.Length == 0)
            {
                Debug.LogWarning("LODGroup has no LOD levels.");
                return;
            }

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            var combineDict = new Dictionary<(Material, Transform), CombinedLodsRenderer>();
            CollectCombineInstances(lods, combineDict);

            if (combineDict.Count == 0)
            {
                Debug.LogWarning("No valid meshes found to combine.");
                return;
            }

            foreach (CombinedLodsRenderer combinedMeshRenderer in combineDict.Values)
            {
                CombinedLodsRenderers.Add(combinedMeshRenderer);
                SaveCombinedMeshAsSubAsset(combinedMeshRenderer.CombinedMesh);
            }

            // LOD Group
            Reference = lodGroup;
            Transform = lodGroup.transform;
            Name = lodGroup.transform.name;

            lodGroup.RecalculateBounds();
            ObjectSize = lodGroup.size;
            LodsScreenSpaceSizes = new float [lods.Length];

            for (var i = 0; i < lods.Length && i < MAX_LODS_LEVEL; i++)
                LodsScreenSpaceSizes[i] = lods[i].screenRelativeTransitionHeight;

            UpdateBoundsByCombinedLods();

            AssetDatabase.SaveAssets();
        }

        private void CollectCombineInstances(LOD[] lods, Dictionary<(Material, Transform), CombinedLodsRenderer> combineDict)
        {
            foreach (LOD lod in lods)
            foreach (Renderer rend in lod.renderers)
            {
                if (rend is not MeshRenderer)
                {
                    Debug.LogWarning($"Renderer '{rend.name}' is missing in LODGroup assigned renderers.");
                    continue;
                }

                MeshFilter mf = rend.GetComponent<MeshFilter>();

                if (mf == null || mf.sharedMesh == null)
                {
                    Debug.LogWarning($"Renderer '{rend.name}' is missing a MeshFilter or its mesh.");
                    continue;
                }

                for (var subMeshIndex = 0; subMeshIndex < rend.sharedMaterials.Length; subMeshIndex++)
                {
                    Material mat = rend.sharedMaterials[subMeshIndex];

                    if (mat == null || !IsInWhitelist(mat, whitelistedShaders))
                    {
                        Debug.LogWarning($"Renderer '{rend.name}' does not have a material or has not valid shader.");
                        continue;
                    }

                    var ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = subMeshIndex,

                        // Convert the renderer's transform into the local space of the prefab root.
                        transform = rend.transform.localToWorldMatrix * transform.worldToLocalMatrix,
                    };

                    (Material mat, Transform parent) key = (mat, rend.transform.parent);

                    if (!combineDict.ContainsKey(key))
                        combineDict[key] = new CombinedLodsRenderer(mat, rend);

                    // NOTE (Vit): it can add equal meshes, but how otherwise we can treat LOD inside compute shader?
                    combineDict[key].AddCombineInstance(ci, rend);
                }
            }
        }

        private void SaveCombinedMeshAsSubAsset(Mesh combinedMesh)
        {
            string assetPath = AssetDatabase.GetAssetPath(this);

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("Selected object is not a prefab asset. The combined mesh will not be saved as a sub-asset.");
                return;
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (Object asset in allAssets)
            {
                if (asset is Mesh && asset.name == combinedMesh.name)
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    DestroyImmediate(asset, true);
                }
            }

            AssetDatabase.AddObjectToAsset(combinedMesh, assetPath);
            Debug.Log($"Combined mesh saved as a sub-asset in: {assetPath}");
        }

        public void UpdateBoundsByCombinedLods()
        {
            var isInitialized = false;

            foreach (CombinedLodsRenderer lodsRenderer in CombinedLodsRenderers)
            {
                if (!isInitialized)
                {
                    Bounds = lodsRenderer.CombinedMesh.bounds;
                    isInitialized = true;
                }
                else Bounds.Encapsulate(lodsRenderer.CombinedMesh.bounds);
            }
        }

        private static bool IsInWhitelist(Material material, Shader[] whitelistShaders)
        {
            if (whitelistShaders == null || whitelistShaders.Length == 0)
            {
                Debug.LogError("No whitelist shaders defined!");
                return false;
            }

            return whitelistShaders.Where(shader => shader != null).Any(shader => material.shader == shader || material.shader.name == shader.name || material.shader.name.StartsWith(shader.name) || shader.name.StartsWith(material.shader.name));
        }

        public bool Equals(GPUInstancingLODGroup other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (AreSameNestedPrefabInstance(gameObject, other.gameObject)) return true;

            if (Bounds == other.Bounds
                && LodsScreenSpaceSizes.Length == other.LodsScreenSpaceSizes.Length
                && LodsScreenSpaceSizes.SequenceEqual(other.LodsScreenSpaceSizes)
                && CombinedLodsRenderers.Count == other.CombinedLodsRenderers.Count
                && CombinedLodsRenderers[0].CombinedMesh == other.CombinedLodsRenderers[0].CombinedMesh
                && CombinedLodsRenderers[0].SharedMaterial == other.CombinedLodsRenderers[0].SharedMaterial)
                return true;

            return false;
        }

        public bool AreSameNestedPrefabInstance(GameObject obj1, GameObject obj2)
        {
            GameObject prefabRoot1 = PrefabUtility.GetNearestPrefabInstanceRoot(obj1);
            GameObject prefabRoot2 = PrefabUtility.GetNearestPrefabInstanceRoot(obj2);

            if (prefabRoot1 == null || prefabRoot2 == null)
                return false;

            GameObject prefabAsset1 = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot1);
            GameObject prefabAsset2 = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot2);

            return prefabAsset1 == prefabAsset2;
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingLODGroup);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(base.GetHashCode());
            hashCode.Add(Name);
            hashCode.Add(ObjectSize);
            hashCode.Add(Bounds);
            hashCode.Add(LodsScreenSpaceSizes);
            hashCode.Add(CombinedLodsRenderers);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(GPUInstancingLODGroup left, GPUInstancingLODGroup right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingLODGroup left, GPUInstancingLODGroup right) =>
            !(left == right);
    }
}
