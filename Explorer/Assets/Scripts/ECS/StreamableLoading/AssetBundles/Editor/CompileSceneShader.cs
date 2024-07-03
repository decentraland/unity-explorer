using System.IO;
using UnityEditor;
using UnityEngine;
using Utility;

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
            string sPlatform = PlatformUtils.GetPlatform();
            BuildTarget bt = BuildTarget.StandaloneWindows64; // default
            switch (sPlatform)
            {
                case "_windows":
                {
                    bt = BuildTarget.StandaloneWindows64;
                    break;
                }
                case "_mac":
                {
                    bt = BuildTarget.StandaloneOSX;
                    break;
                }
                case "_linux":
                {
                    bt = BuildTarget.StandaloneLinux64;
                    break;
                }
            }

            CompileTheSceneShader(bt);
        }

        public static void CompileTheSceneShader(BuildTarget bt)
        {
            // Set the name of the asset bundle
            string shaderAssetName = "Scene";
            string assetVariant = "SceneVariants";
            string bundleName = "scene_ignore";
            switch (bt)
            {
                case BuildTarget.StandaloneWindows64:
                {
                    bundleName += "_windows";
                    break;
                }
                case BuildTarget.StandaloneOSX:
                {
                    bundleName += "_mac";
                    break;
                }
                case BuildTarget.StandaloneLinux64:
                {
                    bundleName += "_linux";
                    break;
                }
            }

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
            AssetImporter.GetAtPath(shaderVariantAssetPath).SetAssetBundleNameAndVariant(string.Empty, string.Empty);
            AssetDatabase.RemoveUnusedAssetBundleNames();

            Debug.Log("Asset bundle build and copy process completed.");
        }
    }
}
