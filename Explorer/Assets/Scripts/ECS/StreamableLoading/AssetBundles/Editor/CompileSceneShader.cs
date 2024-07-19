using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using Utility;
using BuildCompression = UnityEngine.BuildCompression;

namespace DCL.Rendering.Menus
{
    public static class CompileSceneShader
    {
        private const string ASSET_BUNDLE_DIRECTORY = "Assets/StreamingAssets/AssetBundles";

        private static readonly string[] TARGET_DIRECTORIES =
        {
            "Assets/StreamingAssets/AssetBundles/lods",
            "Assets/StreamingAssets/AssetBundles/Wearables",
        };

        private static readonly string[] ASSET_NAMES =
        {
            "Scene.shader", "SceneVariants.shadervariants", "SceneVariantsManuallyAdded.shadervariants",
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
            var bundleName = "dcl/scene_ignore";

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

            const string PATH = "Assets/git-submodules/unity-shared-dependencies/Runtime/Shaders/SceneRendering/";

            var importers = new List<AssetImporter>();

            // Mark assets for inclusion in the asset bundle

            foreach (string asset in ASSET_NAMES)
            {
                var importer = AssetImporter.GetAtPath(PATH + asset);
                importer.SetAssetBundleNameAndVariant(bundleName, "");
                importer.SaveAndReimport();
                importers.Add(importer);
            }

            // Create the directory if it doesn't exist
            if (!Directory.Exists(ASSET_BUNDLE_DIRECTORY))
                Directory.CreateDirectory(ASSET_BUNDLE_DIRECTORY);

            // Build the asset bundle
            Debug.Log("assetBundleDirectory: " + ASSET_BUNDLE_DIRECTORY);

            AssetBundleBuild[] buildInput = ContentBuildInterface.GenerateAssetBundleBuilds();

            // Address by names instead of paths for backwards compatibility.
            for (var i = 0; i < buildInput.Length; i++)
                buildInput[i].addressableNames = buildInput[i].assetNames.Select(Path.GetFileName).ToArray();

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(bt);
            var parameters = new BundleBuildParameters(bt, group, ASSET_BUNDLE_DIRECTORY);

            parameters.AppendHash = false;
            parameters.BundleCompression = BuildCompression.Uncompressed;
            parameters.DisableVisibleSubAssetRepresentations = true;

            ContentPipeline.BuildAssetBundles(parameters, new BundleBuildContent(buildInput), out _);

            AssetDatabase.Refresh();

            // Copy the asset bundle to target directories
            string sourceFilePath = Path.Combine(ASSET_BUNDLE_DIRECTORY, bundleName);

            foreach (string targetDirectory in TARGET_DIRECTORIES)
            {
                if (!Directory.Exists(targetDirectory)) { Directory.CreateDirectory(targetDirectory); }

                string targetFilePath = Path.Combine(targetDirectory, bundleName);
                File.Copy(sourceFilePath, targetFilePath, true);
            }

            // Remove the asset bundle mark
            foreach (AssetImporter assetImporter in importers)
                assetImporter.SetAssetBundleNameAndVariant(string.Empty, string.Empty);

            AssetDatabase.RemoveUnusedAssetBundleNames();

            Debug.Log("Asset bundle build and copy process completed.");
        }
    }
}
