using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.Rendering.Menus
{
    public static class CompileSceneShader
    {
        private const string assetBundleDirectory = "Assets/StreamingAssets/AssetBundles/dcl";
        private static readonly string[] targetDirectories = new string[]
        {
            "Assets/StreamingAssets/AssetBundles/lods/dcl",
            "Assets/StreamingAssets/AssetBundles/Wearables/dcl"
        };

        [MenuItem("Decentraland/Shaders/Compile \"Scene\" Shader Variants")]
        public static void ExecuteMenuItem()
        {
            // Set the name of the asset bundle
            string shaderAssetName = "Scene";
            string assetVariant = "SceneVariants";
            string bundleName = "scene_ignore_windows";

            // Mark the asset for inclusion in the asset bundle
            string shaderAssetPath = "Assets/git-submodules/unity-shared-dependencies/Runtime/Shaders/SceneRendering/" + shaderAssetName + ".shader";
            AssetImporter.GetAtPath(shaderAssetPath).SetAssetBundleNameAndVariant(bundleName, "");
            string shaderVariantAssetPath = "Assets/git-submodules/unity-shared-dependencies/Runtime/Shaders/SceneRendering/" + assetVariant + ".shadervariants";
            AssetImporter.GetAtPath(shaderVariantAssetPath).SetAssetBundleNameAndVariant(bundleName, "");

            // Create the directory if it doesn't exist
            if (!Directory.Exists(assetBundleDirectory))
            {
                Directory.CreateDirectory(assetBundleDirectory);
            }

            // Build the asset bundle
            Debug.Log("assetBundleDirectory: " + assetBundleDirectory);
            BuildPipeline.BuildAssetBundles(assetBundleDirectory,
                BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.AssetBundleStripUnityVersion,
                BuildTarget.StandaloneWindows);

            AssetDatabase.Refresh();

            // Copy the asset bundle to target directories
            string sourceFilePath = Path.Combine(assetBundleDirectory, bundleName);
            Debug.Log("sourceFilePath: " + sourceFilePath);
            foreach (string targetDirectory in targetDirectories)
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                string targetFilePath = Path.Combine(targetDirectory, bundleName);
                File.Copy(sourceFilePath, targetFilePath, true);
                //FileUtil.CopyFileOrDirectory(sourceFilePath, targetFilePath);
            }

            // Remove the asset bundle mark
            AssetImporter.GetAtPath(shaderAssetPath).SetAssetBundleNameAndVariant(string.Empty, string.Empty);
            AssetImporter.GetAtPath(assetVariant).SetAssetBundleNameAndVariant(string.Empty, string.Empty);
            AssetDatabase.RemoveUnusedAssetBundleNames();

            Debug.Log("Asset bundle build and copy process completed.");
        }
    }
}
