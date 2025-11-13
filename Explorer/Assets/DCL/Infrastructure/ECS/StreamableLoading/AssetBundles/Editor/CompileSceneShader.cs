using DCL.Utility;
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

        private static readonly string[] ASSET_NAMES =
        {
            "Scene.shader", "SceneVariants.shadervariants",
        };

        // Add file extensions to copy when copying shader folder
        private static readonly string[] SHADER_FILE_EXTENSIONS =
        {
            ".shader", ".shadervariants", ".hlsl", ".cginc", ".glslinc", ".compute", ".cg"
        };

        [MenuItem("Decentraland/Shaders/Compile \"Scene\" Shader Variants")]
        public static void ExecuteMenuItem()
        {
            string sPlatform = PlatformUtils.GetCurrentPlatform();
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
            }

            CompileTheSceneShader(bt);
        }

        [MenuItem("Decentraland/Shaders/Force Recompile \"Scene\" Shader Variants")]
        public static void ForceRecompileMenuItem()
        {
            string sPlatform = PlatformUtils.GetCurrentPlatform();
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
            }

            CompileTheSceneShader(bt, forceRecompile: true);
        }

        public static void CompileTheSceneShader(BuildTarget bt, bool forceRecompile = false)
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

            // Try multiple paths in order of preference:
            // 1. Local embedded package (if you've made it local)
            // 2. Project Assets folder (if you've copied shaders there)
            // 3. Original package location

            string shaderPath = null;
            List<string> searchPaths = new List<string>
            {
                // Check if shaders have been copied to Assets for local editing
                "Assets/Shaders/Scene/SceneRendering/",
                "Assets/DCL/Shaders/Scene/SceneRendering/",

                // Check embedded/local package in Packages folder
                "Packages/com.decentraland.unity-shared-dependencies/Runtime/Shaders/Scene/SceneRendering/",

                // Check if package has been made local/embedded
                "Packages/com.decentraland.unity-shared-dependencies-local/Runtime/Shaders/Scene/SceneRendering/",
            };

            // Also check the dynamic package path
            string packagePath = GetPackagePath("com.decentraland.unity-shared-dependencies");
            if (!string.IsNullOrEmpty(packagePath))
            {
                searchPaths.Add(Path.Combine(packagePath, "Runtime/Shaders/Scene/SceneRendering/"));
            }

            // Find the first valid path
            foreach (string path in searchPaths)
            {
                if (Directory.Exists(path))
                {
                    bool hasAllFiles = true;
                    foreach (string asset in ASSET_NAMES)
                    {
                        if (!File.Exists(Path.Combine(path, asset)))
                        {
                            hasAllFiles = false;
                            break;
                        }
                    }

                    if (hasAllFiles)
                    {
                        shaderPath = path;
                        Debug.Log($"Found shaders at: {shaderPath}");
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogError("Could not find shader files. Searched in:\n" + string.Join("\n", searchPaths));
                Debug.LogError("Consider copying the shaders to your Assets folder for local editing using: Decentraland > Shaders > Copy Package Shaders to Assets");
                return;
            }

            var importers = new List<AssetImporter>();

            // Mark assets for inclusion in the asset bundle
            foreach (string asset in ASSET_NAMES)
            {
                string fullAssetPath = Path.Combine(shaderPath, asset);

                var importer = AssetImporter.GetAtPath(fullAssetPath);

                if (importer == null)
                {
                    Debug.LogError($"Could not get importer for asset: {fullAssetPath}");
                    continue;
                }

                // Force reimport if requested (to pick up local changes)
                if (forceRecompile)
                {
                    Debug.Log($"Force reimporting: {fullAssetPath}");
                    // Also reimport any include files in the same directory
                    ReimportShaderIncludes(shaderPath);
                    AssetDatabase.ImportAsset(fullAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                    // Re-get the importer after forced import
                    importer = AssetImporter.GetAtPath(fullAssetPath);
                }

                importer.SetAssetBundleNameAndVariant(bundleName, "");
                importer.SaveAndReimport();
                importers.Add(importer);
            }

            if (importers.Count == 0)
            {
                Debug.LogError("No assets were successfully marked for bundling.");
                return;
            }

            // Clear any cached shader compilation before building
            if (forceRecompile)
            {
                Debug.Log("Clearing shader cache...");
                // Clear shader messages for any loaded Scene shader
                Shader sceneShader = Shader.Find("Scene");
                if (sceneShader != null)
                {
                    ShaderUtil.ClearShaderMessages(sceneShader);
                }

                // Clear the shader cache by reimporting
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Shader.WarmupAllShaders();
            }

            // Create the directory if it doesn't exist
            if (!Directory.Exists(ASSET_BUNDLE_DIRECTORY))
                Directory.CreateDirectory(ASSET_BUNDLE_DIRECTORY);

            // Build the asset bundle
            Debug.Log("Building asset bundle to: " + ASSET_BUNDLE_DIRECTORY);

            // Delete existing bundle to force rebuild
            string existingBundle = Path.Combine(ASSET_BUNDLE_DIRECTORY, bundleName);
            if (File.Exists(existingBundle))
            {
                Debug.Log($"Deleting existing bundle: {existingBundle}");
                File.Delete(existingBundle);
            }

            AssetBundleBuild[] buildInput = ContentBuildInterface.GenerateAssetBundleBuilds();

            // Address by names instead of paths for backwards compatibility.
            for (var i = 0; i < buildInput.Length; i++)
                buildInput[i].addressableNames = buildInput[i].assetNames.Select(Path.GetFileName).ToArray();

            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(bt);
            var parameters = new BundleBuildParameters(bt, group, ASSET_BUNDLE_DIRECTORY);

            parameters.AppendHash = false;
            parameters.BundleCompression = BuildCompression.Uncompressed;
            parameters.DisableVisibleSubAssetRepresentations = true;

            // Force rebuild
            parameters.UseCache = !forceRecompile;

            var result = ContentPipeline.BuildAssetBundles(parameters, new BundleBuildContent(buildInput), out var buildResults);

            if (result == ReturnCode.Success)
            {
                Debug.Log("Asset bundle build successful!");
                // Log bundle info if available
                var bundleNames = buildInput.Select(b => b.assetBundleName).ToArray();
                foreach (var bundle in bundleNames)
                {
                    Debug.Log($"Built bundle: {bundle}");
                }
            }
            else
            {
                Debug.LogError($"Asset bundle build failed with code: {result}");
            }

            AssetDatabase.Refresh();

            // Copy the asset bundle to target directories
            string sourceFilePath = Path.Combine(ASSET_BUNDLE_DIRECTORY, bundleName);

            // Remove the asset bundle mark
            foreach (AssetImporter assetImporter in importers)
                assetImporter.SetAssetBundleNameAndVariant(string.Empty, string.Empty);

            AssetDatabase.RemoveUnusedAssetBundleNames();

            Debug.Log("Asset bundle build and copy process completed.");

            // Verify the bundle was created
            if (File.Exists(sourceFilePath))
            {
                var fileInfo = new FileInfo(sourceFilePath);
                Debug.Log($"Bundle created successfully: {sourceFilePath} ({fileInfo.Length} bytes)");
            }
            else
            {
                Debug.LogWarning($"Bundle file not found after build: {sourceFilePath}");
            }
        }

        /// <summary>
        /// Reimports all shader include files in the specified directory and subdirectories
        /// </summary>
        private static void ReimportShaderIncludes(string shaderPath)
        {
            // Get the parent directory to ensure we catch all includes
            string parentPath = Directory.GetParent(shaderPath)?.FullName ?? shaderPath;

            if (Directory.Exists(parentPath))
            {
                var includeFiles = Directory.GetFiles(parentPath, "*.hlsl", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(parentPath, "*.cginc", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(parentPath, "*.glslinc", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(parentPath, "*.cg", SearchOption.AllDirectories));

                foreach (var includeFile in includeFiles)
                {
                    string relativePath = includeFile.Replace('\\', '/');
                    if (relativePath.StartsWith(Application.dataPath))
                    {
                        relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
                    }

                    Debug.Log($"Force reimporting include: {relativePath}");
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        /// <summary>
        /// Gets the file system path for a Unity package.
        /// </summary>
        private static string GetPackagePath(string packageName)
        {
            // Method 1: Try the Packages folder (for locally referenced packages)
            string packagesPath = $"Packages/{packageName}";
            if (Directory.Exists(packagesPath))
            {
                return packagesPath;
            }

            // Method 2: Try using PackageManager to get the resolved path
            var listRequest = UnityEditor.PackageManager.Client.List(true);
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                foreach (var package in listRequest.Result)
                {
                    if (package.name == packageName)
                    {
                        Debug.Log($"Package '{packageName}' found at: {package.resolvedPath}");
                        return package.resolvedPath;
                    }
                }
            }

            // Method 3: Try Library/PackageCache (for registry packages)
            string[] packageCachePaths = Directory.GetDirectories("Library/PackageCache", $"{packageName}@*");
            if (packageCachePaths.Length > 0)
            {
                return packageCachePaths[0];
            }

            return null;
        }

        [MenuItem("Decentraland/Shaders/Copy Package Shaders to Assets (Full Folder with Includes)")]
        public static void CopyPackageShadersToAssets()
        {
            string packagePath = GetPackagePath("com.decentraland.unity-shared-dependencies");
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError("Could not find package 'com.decentraland.unity-shared-dependencies'");
                return;
            }

            // Copy the entire Shaders folder structure
            string sourceShaderRoot = Path.Combine(packagePath, "Runtime/Shaders");
            string targetShaderRoot = "Assets/DCL/Shaders";

            if (!Directory.Exists(sourceShaderRoot))
            {
                Debug.LogError($"Source shader directory not found: {sourceShaderRoot}");
                return;
            }

            // Copy the entire shader directory recursively
            CopyDirectory(sourceShaderRoot, targetShaderRoot, true);

            AssetDatabase.Refresh();

            Debug.Log($"Successfully copied entire shader folder structure from:\n{sourceShaderRoot}\nto:\n{targetShaderRoot}");
            Debug.Log("You can now edit the shaders and includes locally, and they will be used for compilation.");

            // List what was copied
            int fileCount = 0;
            foreach (var ext in SHADER_FILE_EXTENSIONS)
            {
                var files = Directory.GetFiles(targetShaderRoot, $"*{ext}", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    Debug.Log($"Copied {files.Length} {ext} files");
                    fileCount += files.Length;
                }
            }
            Debug.Log($"Total files copied: {fileCount}");
        }

        [MenuItem("Decentraland/Shaders/Copy Only Scene Shader Folder to Assets")]
        public static void CopySceneShaderFolderToAssets()
        {
            string packagePath = GetPackagePath("com.decentraland.unity-shared-dependencies");
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError("Could not find package 'com.decentraland.unity-shared-dependencies'");
                return;
            }

            // Copy just the Scene folder and its includes
            string sourceSceneFolder = Path.Combine(packagePath, "Runtime/Shaders/Scene");
            string targetSceneFolder = "Assets/DCL/Shaders/Scene";

            if (!Directory.Exists(sourceSceneFolder))
            {
                Debug.LogError($"Source Scene shader directory not found: {sourceSceneFolder}");
                return;
            }

            // Copy the Scene directory recursively
            CopyDirectory(sourceSceneFolder, targetSceneFolder, true);

            AssetDatabase.Refresh();

            Debug.Log($"Successfully copied Scene shader folder from:\n{sourceSceneFolder}\nto:\n{targetSceneFolder}");

            // List what was copied
            int fileCount = 0;
            foreach (var ext in SHADER_FILE_EXTENSIONS)
            {
                var files = Directory.GetFiles(targetSceneFolder, $"*{ext}", SearchOption.AllDirectories);
                fileCount += files.Length;
            }
            Debug.Log($"Total shader files copied: {fileCount}");
            Debug.Log("The Scene shader and all its includes are now available for local editing.");
        }

        /// <summary>
        /// Recursively copies a directory and all its contents
        /// </summary>
        private static void CopyDirectory(string sourceDir, string targetDir, bool recursive)
        {
            // Get the subdirectories for the specified directory
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDir}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it
            Directory.CreateDirectory(targetDir);

            // Get the files in the directory and copy them to the new location
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                // Only copy shader-related files
                bool shouldCopy = SHADER_FILE_EXTENSIONS.Any(ext =>
                    file.Extension.Equals(ext, System.StringComparison.OrdinalIgnoreCase));

                // Also copy .meta files to preserve import settings
                if (file.Extension.Equals(".meta", System.StringComparison.OrdinalIgnoreCase))
                {
                    string associatedFile = file.FullName.Substring(0, file.FullName.Length - 5);
                    FileInfo associatedFileInfo = new FileInfo(associatedFile);
                    if (associatedFileInfo.Exists)
                    {
                        shouldCopy = SHADER_FILE_EXTENSIONS.Any(ext =>
                            associatedFileInfo.Extension.Equals(ext, System.StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (shouldCopy)
                {
                    string tempPath = Path.Combine(targetDir, file.Name);
                    file.CopyTo(tempPath, true);
                    Debug.Log($"Copied: {file.Name}");
                }
            }

            // If copying subdirectories, copy them and their contents to new location
            if (recursive)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(targetDir, subdir.Name);
                    CopyDirectory(subdir.FullName, tempPath, recursive);
                }
            }
        }
    }
}
