using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{


    [Serializable]
    public class GPUInstancingCandidate : MonoBehaviour, IEquatable<GPUInstancingCandidate>
    {
        private const int MAX_LODS_LEVEL = 8;
        public Shader[] whitelistedShaders;

        [Header("REFERENCES")]
        public string Name;
        public LODGroup Reference;
        public Transform Transform;

        [Header("BUFFERS")]
        public List<PerInstanceBuffer> InstancesBuffer;
        public List<Matrix4x4> InstancesBufferDirect;

        [Header("LOD GROUP DATA")]
        public float ObjectSize;
        public Bounds Bounds;
        public float[] LodsScreenSpaceSizes;

        [Space]
        public List<LodsCombinedMesh> CombinedLods;
        public List<GPUInstancingLodLevel> Lods;

        #if UNITY_EDITOR
        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            LODGroup lodGroup = this.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                Debug.LogWarning("Selected GameObject does not have a LODGroup component.");
                return;
            }

            LOD[] lods = lodGroup.GetLODs();
            if(lods.Length == 0)
            {
                Debug.LogWarning("LODGroup has no LOD levels.");
                return;
            }

            // Position at origin
            this.transform.position = Vector3.zero;
            this.transform.rotation = Quaternion.identity;
            this.transform.localScale = Vector3.one;

            Dictionary<Material, LodsCombinedMesh> combineDict = new Dictionary<Material, LodsCombinedMesh>();
            CollectCombineInstances(lods, combineDict);

            if (combineDict.Count == 0)
            {
                Debug.LogWarning("No valid meshes found to combine.");
                return;
            }

            foreach (LodsCombinedMesh combinedMesh in combineDict.Values)
            {
                Mesh groupMesh = new Mesh();
                // The 'true' flags here tell Unity to merge submeshes and transform vertices.
                groupMesh.CombineMeshes(combinedMesh.СombineInstances.ToArray(), true, true);
                groupMesh.name = $"{combinedMesh.parent.name}_{combinedMesh.SharedMaterial.name}";

                // Save as sub-asset
                string assetPath = AssetDatabase.GetAssetPath(this);
                if (string.IsNullOrEmpty(assetPath))
                    Debug.LogWarning("Selected object is not a prefab asset. The combined mesh will not be saved as a sub-asset.");
                else
                {
                    AssetDatabase.AddObjectToAsset(groupMesh, assetPath);
                    Debug.Log($"Combined mesh saved as a sub-asset in: {assetPath}");
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void SaveMeshSeparately(LodsCombinedMesh combinedMesh, Mesh groupMesh)
        {
            var folderPath = "Assets/DCL/Rendering/GPUInstancing/CombinedMeshes";
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets", "CombinedMeshes");

            // Generate a unique asset path for the combined mesh.
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{combinedMesh.parent.name}_{combinedMesh.SharedMaterial.name}.asset");
            AssetDatabase.CreateAsset(groupMesh, assetPath);
            Debug.Log($"Combined mesh saved as asset at: {assetPath}");
        }

        [ContextMenu(nameof(RemoveAllSubAssetsMenu))]
        private void RemoveAllSubAssetsMenu()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("No asset selected. Please select the asset from which you want to remove empty sub-assets.");
                return;
            }

            // Load the main asset and all associated assets at this path.
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            var removedCount = 0;
            foreach (var subAsset in allAssets)
            {
                // Skip the main asset.
                if (subAsset == mainAsset) continue;

                AssetDatabase.RemoveObjectFromAsset(subAsset);
                DestroyImmediate(subAsset, true);
                removedCount++;
            }

            // Save changes if any sub-assets were removed.
            if (removedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Removed {removedCount} empty sub-asset(s) from: {assetPath}");
            }
            else
                Debug.Log("No empty sub-assets found.");
        }

        private void CollectCombineInstances(LOD[] lods, Dictionary<Material, LodsCombinedMesh> combineDict)
        {
            foreach (LOD lod in lods)
            foreach (Renderer rend in lod.renderers)
            {
                if (rend == null)
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

                    if (mat == null)
                    {
                        Debug.LogWarning($"Renderer '{rend.name}' does not have a material.");
                        continue;
                    }

                    CombineInstance ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = subMeshIndex,
                        // Convert the renderer's transform into the local space of the prefab root.
                        transform = rend.transform.localToWorldMatrix * this.transform.worldToLocalMatrix,
                    };

                    if (!combineDict.ContainsKey(mat))
                        combineDict[mat] = new LodsCombinedMesh(ci, mat, rend);

                    combineDict[mat].AddCombineInstance(ci, rend);
                }
            }
        }
#endif


        public GPUInstancingCandidate(GPUInstancingCandidate candidate)
        {
            InstancesBuffer = candidate.InstancesBuffer;

            ObjectSize = candidate.ObjectSize;
            Bounds = candidate.Bounds;

            LodsScreenSpaceSizes = candidate.LodsScreenSpaceSizes;
            Lods = candidate.Lods;
        }

        public GPUInstancingCandidate(GPUInstancingCandidate candidate, HashSet<PerInstanceBuffer> instanceBuffers)
        {
            Name = candidate.Name;
            InstancesBuffer = instanceBuffers.ToList();

            ObjectSize = candidate.ObjectSize;
            Bounds = candidate.Bounds;

            LodsScreenSpaceSizes = candidate.LodsScreenSpaceSizes;
            Lods = candidate.Lods;
        }

        public GPUInstancingCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix, Shader[] whitelistShaders)
        {
            this.whitelistedShaders = whitelistShaders;

            Reference = lodGroup;
            Transform = lodGroup.transform;
            Name = lodGroup.transform.name;

            LOD[] lodLevels = lodGroup.GetLODs();
            lodGroup.RecalculateBounds();

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

            ObjectSize = lodGroup.size;

            LodsScreenSpaceSizes = new float [lodLevels.Length];
            Lods = new List<GPUInstancingLodLevel>();

            for (var i = 0; i < lodLevels.Length && i < MAX_LODS_LEVEL; i++)
            {
                LOD lod = lodLevels[i];

                LodsScreenSpaceSizes[i] = lod.screenRelativeTransitionHeight;

                var lodMeshes = new List<MeshRenderingData>();

                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer is MeshRenderer meshRenderer && renderer.sharedMaterial != null && IsValidShader(meshRenderer.sharedMaterials, whitelistedShaders))
                        lodMeshes.Add(new MeshRenderingData(meshRenderer));
                }

                if (lodMeshes.Count > 0)
                    Lods.Add(new GPUInstancingLodLevel { MeshRenderingDatas = lodMeshes.ToArray() });
            }

            UpdateBounds();
        }

        public GPUInstancingCandidate(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, Shader[] whitelistShaders)
        {
            this.whitelistedShaders = whitelistShaders;

            if (meshRenderer.sharedMaterial == null||!IsValidShader(meshRenderer.sharedMaterials, whitelistedShaders))
                return;

            Reference = null; // No LODGroup
            Transform = meshRenderer.transform;
            Name = Transform.name;

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

            if (meshRenderer.TryGetComponent(out MeshFilter mf))
                ObjectSize = mf.sharedMesh.bounds.extents.magnitude * 2f;
            else
                ObjectSize = 1f;

            LodsScreenSpaceSizes = new[] { 1.0f }; // Single LOD => We only have 1 screen space size and 1 LOD level
            Lods = new List<GPUInstancingLodLevel>(1); // only 1 lod level

            var singleLodMeshes = new List<MeshRenderingData> { new (meshRenderer) };
            Lods.Add(new GPUInstancingLodLevel { MeshRenderingDatas = singleLodMeshes.ToArray() });

            UpdateBounds();
        }

        public static bool IsValidShader(Material[] materials, Shader[] whitelistShaders)
        {
            if (materials == null || materials.Length == 0) return false;

            foreach (Material m in materials)
            {
                if (m == null)
                    return false;

                if (!IsInWhitelist(m, whitelistShaders))
                {
                    Debug.LogWarning($"Material {m.name} uses non-whitelisted shader: {m.shader.name}");
                    return false;
                }
            }

            return true;
        }

        private static bool IsInWhitelist(Material material, Shader[] whitelistShaders)
        {
            if (whitelistShaders == null || whitelistShaders.Length == 0)
            {
                Debug.LogError("No whitelist shaders defined!");
                return false;
            }

            return whitelistShaders.Where(shader => shader != null).
                                      Any(shader => material.shader == shader || material.shader.name == shader.name || material.shader.name.StartsWith(shader.name) || shader.name.StartsWith(material.shader.name));
        }

        public void PopulateDirectInstancingBuffer()
        {
            InstancesBufferDirect = new List<Matrix4x4>(InstancesBuffer.Count);

            for (var i = 0; i < InstancesBuffer.Count; i++)
                InstancesBufferDirect.Add(InstancesBuffer[i].instMatrix);
        }

        // TODO (Vit): calculate bounds properly
        public void UpdateBounds()
        {
            var isInitialized = false;

            foreach (GPUInstancingLodLevel lodLevel in Lods)
            foreach (MeshRenderingData data in lodLevel.MeshRenderingDatas)
            {
                if (!isInitialized)
                {
                    Bounds = data.SharedMesh.bounds;
                    isInitialized = true;
                }
                else Bounds.Encapsulate(data.SharedMesh.bounds);
            }
        }

        /// <summary>
        ///     Returns true if the rendering data (all LOD levels and, for each level, each MeshRenderingData's SharedMesh and SharedMaterial) is the same between this candidate and the other.
        /// </summary>
        public bool Equals(GPUInstancingCandidate other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Lods.Count != other.Lods.Count)
                return false;

            for (var i = 0; i < Lods.Count; i++)
            {
                // if (Lods[i].MeshRenderingDatas == null || other.Lods[i].MeshRenderingDatas == null)
                // {
                //     if (Lods[i].MeshRenderingDatas != other.Lods[i].MeshRenderingDatas)
                //         return false;
                // }
                // else
                if (!Lods[i].MeshRenderingDatas.SequenceEqual(other.Lods[i].MeshRenderingDatas))
                    return false;
            }

            // for (var i = 0; i < Lods.Count; i++)
            // {
            //     var myData = Lods[i].MeshRenderingDatas;
            //     var otherData = other.Lods[i].MeshRenderingDatas;
            //
            //     if (myData.Length != otherData.Length)
            //         return false;
            //
            //     for (var j = 0; j < myData.Length; j++)
            //         if (!myData[j].Equals(otherData[j]))
            //             return false;
            // }

            return true;
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingCandidate);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                if (Lods != null)
                    hash = Lods.Where(lod => lod.MeshRenderingDatas != null)
                               .SelectMany(lod => lod.MeshRenderingDatas)
                               .Aggregate(hash, (current, data) => (current * 23) + (data?.GetHashCode() ?? 0));

                return hash;
            }
        }

        public static bool operator ==(GPUInstancingCandidate left, GPUInstancingCandidate right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingCandidate left, GPUInstancingCandidate right) =>
            !(left == right);
    }

    [Serializable]
    public class GPUInstancingLodLevel
    {
        public MeshRenderingData[] MeshRenderingDatas;
    }
}
