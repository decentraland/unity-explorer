using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class GPUI_Asset_Generator : MonoBehaviour
{
    public TerrainGenerationData genData;

#if UNITY_EDITOR
    [ContextMenu("Generate")]
    public void Generate()
    {
        const string folderName = "GPUI_Juani";
        const string shaderPath = "Universal Render Pipeline/Nature/SpeedTree8_PBRLit_GPUI_Juani";
        var newShader = Shader.Find(shaderPath);

        if (newShader == null)
            Debug.LogWarning($"Shader not found at \"{shaderPath}\". Please verify its path.");

        foreach (LandscapeAsset entry in genData.treeAssets)
        {
            GameObject? srcPrefab = entry.asset;
            if (srcPrefab == null) continue;

            // 1) Source path & ensure GPUI_Juani folder
            string srcPath = AssetDatabase.GetAssetPath(srcPrefab);
            if (string.IsNullOrEmpty(srcPath)) continue;
            string parentFolder = Path.GetDirectoryName(srcPath).Replace("\\", "/");
            var targetFolder = $"{parentFolder}/{folderName}";

            if (!AssetDatabase.IsValidFolder(targetFolder))
                AssetDatabase.CreateFolder(parentFolder, folderName);

            // 2) Instantiate for material processing
            var instance = PrefabUtility.InstantiatePrefab(srcPrefab) as GameObject;
            if (instance == null) continue;

            // 3) Duplicate & re-shader each material
            foreach (Renderer? rend in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[]? mats = rend.sharedMaterials;

                for (var i = 0; i < mats.Length; i++)
                {
                    Material? mat = mats[i];
                    if (mat == null) continue;

                    // duplicate material asset
                    var matCopy = new Material(mat);
                    if (newShader != null) matCopy.shader = newShader;
                    var matFileName = $"GPUI_Juani_{mat.name}.mat";
                    AssetDatabase.CreateAsset(matCopy, $"{targetFolder}/{matFileName}");

                    mats[i] = matCopy;
                }

                rend.sharedMaterials = mats;
            }

            // 4) Save as a fresh prefab named GPUI_Juani_<OriginalName>_Prefab.prefab
            var newPrefabName = $"GPUI_Juani_Prefab_{srcPrefab.name}.prefab";
            var newPrefabPath = $"{targetFolder}/{newPrefabName}";
            PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
            DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GPUI_Juani prefab generation complete.");
    }
#endif
}
