using UnityEngine;

namespace DCL.Roads.GPUInstancing.Utils
{
    public class MeshCombiner
    {
        public static void SaveCombinedMeshAsSubAsset(Mesh combinedMesh, GameObject gameObject)
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
            Debug.Log($"Combined mesh saved as a sub-asset in: {assetPath}");
#endif
        }
    }
}
