using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
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
        public List<Renderer> RefRenderers = new ();

        [Header("LOD GROUP DATA")]
        public float ObjectSize;
        public Bounds Bounds;
        public float[] LodsScreenSpaceSizes;
        public Matrix4x4 LODSizesMatrix;

        [Space]
        public List<CombinedLodsRenderer> CombinedLodsRenderers;

#if UNITY_EDITOR
        [ContextMenu(nameof(HideAll))]
        public void HideAll()
        {
            foreach (Renderer refRenderer in RefRenderers)
                refRenderer.enabled = false;

            if (Reference == null) return;
            bool isAllRenderersDisabled = Reference.GetLODs().All(lod => lod.renderers.All(lodRenderer => lodRenderer.enabled));
            if (isAllRenderersDisabled) Reference.enabled = false;
        }

        [ContextMenu(nameof(ShowAll))]
        public void ShowAll()
        {
            if(Reference!= null) Reference.enabled = true;

            foreach (Renderer refRenderer in RefRenderers) refRenderer.enabled = true;
        }

        [ContextMenu(nameof(CollectStandaloneRenderers))]
        private void CollectStandaloneRenderers()
        {
            var renderer = GetComponent<MeshRenderer>();
            var meshFilter = GetComponent<MeshFilter>();
            var combinedRenderer = new CombinedLodsRenderer(renderer.sharedMaterial,  renderer,  meshFilter);
            CombinedLodsRenderers = new List<CombinedLodsRenderer> { combinedRenderer };
            RefRenderers.Add(renderer);

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // LOD Group
            Reference = null;
            Transform = this.transform;
            Name = this.transform.name;

            LodsScreenSpaceSizes = new[] { 0.0f }; // Single LOD with maximum visibility
            Bounds = new Bounds();
            Bounds.Encapsulate(meshFilter.sharedMesh.bounds);

            ObjectSize = Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);

            BuildLODMatrix(1);
            AssetDatabase.SaveAssets();
        }

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            if (GetComponentsInChildren<GPUInstancingLODGroup>().Length > 1)
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

            LodsScreenSpaceSizes = new float[lods.Length];

            for (var i = 0; i < lods.Length && i < MAX_LODS_LEVEL; i++)
                LodsScreenSpaceSizes[i] = lods[i].screenRelativeTransitionHeight;

            BuildLODMatrix(lods.Length);

            UpdateBoundsByCombinedLods();

            HideAll();
            AssetDatabase.SaveAssets();
        }

        private void BuildLODMatrix(int lodsLength)
        {
            LODSizesMatrix = new Matrix4x4();
            const float overlapFactor = 0.20f;

            for (var i = 0; i < lodsLength && i < MAX_LODS_LEVEL; i++)
            {
                float endValue = LodsScreenSpaceSizes[i];
                float startValue;

                if (i == 0)
                    startValue = 1f;
                else
                {
                    float prevEnd = LodsScreenSpaceSizes[i - 1];
                    float difference = prevEnd - endValue;
                    float overlap = difference * overlapFactor;

                    startValue = prevEnd + overlap;
                }

                // 4) Write [startValue, endValue] into LODSizesMatrix.
                //    The pattern:
                //      - row0 & row1 for 'start'
                //      - row2 & row3 for 'end'
                //    i < 4 => row0 & row2, i >= 4 => row1 & row3
                int rowStart = i < 4 ? 0 : 1;
                int rowEnd = rowStart + 2; // 2 or 3
                int col = i % 4;

                LODSizesMatrix[rowStart, col] = startValue;
                LODSizesMatrix[rowEnd, col] = endValue;
            }
        }

        private void CollectCombineInstances(LOD[] lods, Dictionary<(Material, Transform), CombinedLodsRenderer> combineDict)
        {
            foreach (LOD lod in lods)
            foreach (Renderer rend  in lod.renderers)
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

                    RefRenderers.Add(rend);
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

        private void UpdateBoundsByCombinedLods()
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
#endif

        public bool Equals(GPUInstancingLODGroup other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;

            // Check if they're instances of the same prefab
            // if (AreSameNestedPrefabInstance(gameObject, other.gameObject)) return true;

            // Check basic properties
            if (Name != other.Name) return false;
            if (Math.Abs(ObjectSize - other.ObjectSize) > 0.001f) return false; // Float comparison with epsilon

            // Check LOD screen space sizes
            if (LodsScreenSpaceSizes == null || other.LodsScreenSpaceSizes == null) return false;
            if (LodsScreenSpaceSizes.Length != other.LodsScreenSpaceSizes.Length) return false;

            // Compare LOD sizes with tolerance
            const float lodSizeTolerance = 0.001f;

            for (var i = 0; i < LodsScreenSpaceSizes.Length; i++)
            {
                if (Math.Abs(LodsScreenSpaceSizes[i] - other.LodsScreenSpaceSizes[i]) > lodSizeTolerance)
                    return false;
            }

            // Check Combined Renderers
            // if (CombinedLodsRenderers == null || other.CombinedLodsRenderers == null) return false;
            // if (CombinedLodsRenderers.Count != other.CombinedLodsRenderers.Count) return false;

            // Compare essential properties of combined renderers
            // for (int i = 0; i < CombinedLodsRenderers.Count; i++)
            // {
            //     var thisRenderer = CombinedLodsRenderers[i];
            //     var otherRenderer = other.CombinedLodsRenderers[i];
            //
            //     // Check if meshes have the same vertex count and submesh count
            //     if (thisRenderer.CombinedMesh.vertexCount != otherRenderer.CombinedMesh.vertexCount)
            //         return false;
            //     if (thisRenderer.CombinedMesh.subMeshCount != otherRenderer.CombinedMesh.subMeshCount)
            //         return false;
            //
            //     // Compare materials
            //     if (thisRenderer.SharedMaterial.shader != otherRenderer.SharedMaterial.shader)
            //         return false;
            // }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Name);
            hashCode.Add(ObjectSize);

            // Add hash of LOD sizes
            if (LodsScreenSpaceSizes != null)
            {
                foreach (float size in LodsScreenSpaceSizes)
                    hashCode.Add(size);
            }

            // Add hash of combined renderers essential properties
            // if (CombinedLodsRenderers != null)
            // {
            //     foreach (var renderer in CombinedLodsRenderers)
            //     {
            //         if (renderer.CombinedMesh != null)
            //         {
            //             hashCode.Add(renderer.CombinedMesh.vertexCount);
            //             hashCode.Add(renderer.CombinedMesh.subMeshCount);
            //         }
            //         if (renderer.SharedMaterial != null && renderer.SharedMaterial.shader != null)
            //             hashCode.Add(renderer.SharedMaterial.shader.name);
            //     }
            // }

            return hashCode.ToHashCode();
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingLODGroup);

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
