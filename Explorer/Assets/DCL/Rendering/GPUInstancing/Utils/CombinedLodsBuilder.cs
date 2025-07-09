using DCL.Rendering.GPUInstancing.InstancingData;
using System.Collections.Generic;
using UnityEditor;
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

        private Mesh CreateCombinedMesh(GameObject parentPrefab)
        {
            var combinedMesh = new Mesh
            {
                name = $"{parent.name}_{sharedMaterial.name}",
            };

            //  mergeSubMeshes == false, so each submesh represents separate LOD level
            combinedMesh.CombineMeshes(combineInstances.ToArray(), mergeSubMeshes: false, useMatrices: true);
            // disable read/write
            combinedMesh.UploadMeshData(true);

            SaveCombinedMeshAsSubAsset(combinedMesh, parentPrefab);

            return combinedMesh;
        }

        private static void SaveCombinedMeshAsSubAsset(Mesh combinedMesh, GameObject gameObject)
        {
#if UNITY_EDITOR
            string assetPath = AssetDatabase.GetAssetPath(gameObject);

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("Selected object is not a prefab asset. The combined mesh will not be saved as a sub-asset.");
                return;
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            foreach (Object asset in allAssets)
            {
                if (asset is Mesh && asset.name.Split('_')[0] == combinedMesh.name.Split('_')[0])
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    Object.DestroyImmediate(asset, true);
                }
            }

            AssetDatabase.AddObjectToAsset(combinedMesh, assetPath);
            Debug.Log($"Combined mesh saved as a sub-asset in: {assetPath}", gameObject);
#endif
        }
    }
}
