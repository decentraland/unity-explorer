using DCL.Diagnostics;
using DCL.Rendering.GPUInstancing.InstancingData;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.Utils
{
    public class CombinedLodsBuilder
    {
        private readonly int subMeshId;
        private readonly Transform parent;
        private readonly Material sharedMaterial;
        private readonly RenderParamsSerialized renderParamsSerialized;

        private readonly List<CombineInstance> combineInstances = new ();

        public CombinedLodsBuilder(Material material, Renderer rend, int subMeshId)
        {
            this.subMeshId = subMeshId;
            parent = rend.transform.parent;
            sharedMaterial = material;
            renderParamsSerialized = new RenderParamsSerialized(rend);
        }

        public void AddCombineInstance(CombineInstance combineInstance) =>
            combineInstances.Add(combineInstance);

        public CombinedLodsRenderer Build(GameObject parentPrefab) =>
            new (sharedMaterial, CreateCombinedMesh(parentPrefab), subMeshId, renderParamsSerialized);

        private (Mesh combinedMesh, LODMeshData[] lodsMeshDataArray) CreateCombinedMesh(GameObject parentPrefab)
        {
            var combinedMesh = new Mesh
            {
                name = $"{parent.name}_{sharedMaterial.name}",
            };

            //  mergeSubMeshes == false, so each submesh represents separate LOD level
            combinedMesh.CombineMeshes(combineInstances.ToArray(), mergeSubMeshes: true, useMatrices: true);
            // disable read/write
            combinedMesh.UploadMeshData(true);

            var lodsMeshDataArray = new LODMeshData[combineInstances.Count];

            uint baseVertexOffset = 0;
            uint startIndexOffset = 0;

            lodsMeshDataArray[0].BaseVertex = 0;
            lodsMeshDataArray[0].StartIndex = 0;
            lodsMeshDataArray[0].IndexCount = combineInstances[0].mesh.GetIndexCount(0);

            for (var i = 1; i < combineInstances.Count; i++)
            {
                CombineInstance instance = combineInstances[i];

                baseVertexOffset += (uint)combineInstances[i-1].mesh.vertices.Length;
                startIndexOffset += combineInstances[i-1].mesh.GetIndexCount(0);

                // Set the offsets for the current mesh
                lodsMeshDataArray[i].BaseVertex = baseVertexOffset;
                lodsMeshDataArray[i].StartIndex = startIndexOffset;
                lodsMeshDataArray[i].IndexCount = instance.mesh.GetIndexCount(0);

                ReportHub.Log(ReportCategory.GPU_INSTANCING, $"LOD Mesh: base = {lodsMeshDataArray[i].BaseVertex} | start = {lodsMeshDataArray[i].StartIndex} | count = {lodsMeshDataArray[i].IndexCount}");
            }

            SaveCombinedMeshAsSubAsset(combinedMesh, parentPrefab);

            return (combinedMesh, lodsMeshDataArray);
        }

        private static void SaveCombinedMeshAsSubAsset(Mesh combinedMesh, GameObject gameObject)
        {
#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(gameObject);

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("Selected object is not a prefab asset. The combined mesh will not be saved as a sub-asset.");
                return;
            }

            Object[] allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (Object asset in allAssets)
                if (asset is Mesh && asset.name == combinedMesh.name)
                {
                    UnityEditor.AssetDatabase.RemoveObjectFromAsset(asset);
                    Object.DestroyImmediate(asset, true);
                }

            UnityEditor.AssetDatabase.AddObjectToAsset(combinedMesh, assetPath);
            Debug.Log($"Combined mesh saved as a sub-asset in: {assetPath}", gameObject);
#endif
        }
    }
}
