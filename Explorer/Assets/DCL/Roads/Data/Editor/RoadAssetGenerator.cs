using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.LOD.Data.Editor
{
    public static class RoadAssetGenerator
    {
        //Builds Roads prefabs from original assets. Use it with caution, asi it will replace your current prefabs
        [MenuItem("Decentraland/Roads/BuildRoadPrefabs")]
        private static void BuildRoadPrefabFromOriginal()
        {
            //BuildRoads();
        }

        private static void BuildRoads()
        {
            string relativeToAssetsPath = "DCL/Roads/Data/RoadAssets/OriginalAssets/";
            var roadModelPath = Path.Combine(Application.dataPath, relativeToAssetsPath);
            string[] files = Directory.GetFiles(roadModelPath, "*.fbx", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (Path.GetExtension(file).Equals(".meta"))
                    continue;

                string assetName = Path.GetFileNameWithoutExtension(file);
                GameObject parentRoad = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/{relativeToAssetsPath}{assetName}/{Path.GetFileName(file)}"));
                
                GameObject instantiatedRoad = parentRoad.transform.GetChild(0).gameObject;
                instantiatedRoad.transform.SetParent(null);
                instantiatedRoad.transform.position = Vector3.zero;
                instantiatedRoad.transform.rotation = Quaternion.identity;
                instantiatedRoad.name = assetName;
                
                Object.DestroyImmediate(parentRoad);
                
                Material sceneMaterial = null;
                foreach (Transform child in instantiatedRoad.GetComponentsInChildren<Transform>())
                {
                    MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
                    if (meshRenderer)
                    {
                        if (sceneMaterial == null)
                        {
                            sceneMaterial = new Material(meshRenderer.material);
                            sceneMaterial.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                            AssetDatabase.CreateAsset(sceneMaterial, $"Assets/{relativeToAssetsPath}{assetName}/Materials/SceneMaterial.mat");
                            AssetDatabase.Refresh();
                        }
                        meshRenderer.sharedMaterial = sceneMaterial;
                    }

                    if (child.name.Contains("_collider")) {
                        MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                        Physics.BakeMesh(meshFilter.sharedMesh.GetInstanceID(), false);
                        meshFilter.gameObject.AddComponent<MeshCollider>();

                        if (meshFilter != null)
                            Object.DestroyImmediate(meshFilter); // Use DestroyImmediate in Editor scripts

                        if (meshRenderer != null)
                            Object.DestroyImmediate(meshRenderer); // Use DestroyImmediate in Editor scripts
                    }
                }
                string assetPath = $"Assets/{relativeToAssetsPath}{assetName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instantiatedRoad, assetPath);
                AssetImporter.GetAtPath(assetPath).SetAssetBundleNameAndVariant("AssetBundles/roads/" + assetName, "");
                Object.DestroyImmediate(instantiatedRoad); // Cleanup
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
