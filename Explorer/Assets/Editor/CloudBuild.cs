using DCL.PerformanceAndDiagnostics.Analytics;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [PublicAPI]
        public static Dictionary<string, object> Parameters { get; private set; }

        private static string SEGMENT_WRITE_KEY = "SEGMENT_WRITE_KEY";

        // Defined in the @T_MacOS/@T_Windows64 configurations in Unity Cloud
        [UsedImplicitly]
        public static void PreExport()
        {
            Debug.Log($"~~ {nameof(CloudBuild)} PreExport ~~");

            // Get all environment variables
            var environmentVariables = Environment.GetEnvironmentVariables();
            Parameters = environmentVariables.Cast<DictionaryEntry>().ToDictionary(x => x.Key.ToString(), x => x.Value);

            // E.g. access like:
            // Debug.Log(Parameters["TEST_VALUE"] as string);
            //

            GenerateIgnoreWarningsFile();

            //Unity suggestion: 1793168
            //This should ensure that the roslyn compiler has been run and everything is generated as needed.
            EditorApplication.ExecuteMenuItem("File/Save Project");

            // Set version for this build
            var buildVersion = Parameters["BUILD_VERSION"] as string;
            PlayerSettings.bundleVersion = buildVersion;
            PlayerSettings.macOS.buildNumber = buildVersion;
            Debug.Log($"Build version set to: {buildVersion}");

            if(Parameters.TryGetValue(SEGMENT_WRITE_KEY, out object segmentKey))
            {
                Debug.Log($"[SEGMENT]: write key found");
                WriteSegmentKeyToAnalyticsConfig(segmentKey as string);
            }
            else
            {
                Debug.Log($"[SEGMENT]: write key not found");
            }

            if (Parameters.TryGetValue("INSTALL_SOURCE", out object source))
            {
                Debug.Log($"[INSTALL_SOURCE]: write key found");
                WriteReleaseStoreToBuildData(source as string);
            }
            else
            {
                Debug.Log($"[INSTALL_SOURCE]: write key not found");
            }

        }

        // Defined in the @T_MacOS/@T_Windows64 configurations in Unity Cloud
        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log($"~~ {nameof(CloudBuild)} PostExport ~~");
        }

        /// <summary>
        /// Remove warnings from the build that are just clutter
        /// We still want to maintain them outside of the build process
        /// </summary>
        private static void GenerateIgnoreWarningsFile()
        {
            // List of warning codes to ignore
            string[] warningsToIgnore = {
                "8618", // Nullable reference types
                "8625", // Cannot convert null literal to non-nullable reference type
                "8602", // Dereference of a possibly null reference
                "8604", // Possible null reference argument
                "8619", // Nullable value types
                "8620", // Argument cannot be used for parameter due to differences in the nullability of reference types
                "8603", // Possible null reference return
                "8600", // Converting null literal or possible null value to non-nullable type
                "8601", // Possible null reference assignment
                "0649", // Field is never assigned to, and will always have its default value
                "0414", // Field is assigned but its value is never used
                "0168", // Variable is declared but never used
                "0219"  // Variable is assigned but its value is never used
            };

            // Path to the Assets folder
            string assetsPath = Application.dataPath;
            string filePath = Path.Combine(assetsPath, "csc.rsp");

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    foreach (string warning in warningsToIgnore)
                    {
                        writer.WriteLine($"-nowarn:{warning}");
                    }
                }

                Debug.Log($"Successfully generated csc.rsp file at: {filePath}");
                Debug.Log($"Added {warningsToIgnore.Length} warning suppressions to the file.");

                // Force a refresh
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to generate csc.rsp file: {ex.Message}");
            }
        }

        private static void WriteReleaseStoreToBuildData(string installSource)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BuildData)}");

            switch (guids.Length)
            {
                case 0:
                    Debug.LogError($"{nameof(BuildData)} asset not found!");
                    return;
                case > 1:
                    Debug.LogWarning($"Multiple {nameof(BuildData)} assets found. Using the first one.");
                    break;
            }

            AssetDatabase.Refresh();
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            BuildData buildData = AssetDatabase.LoadAssetAtPath<BuildData>(assetPath);

            if (buildData != null)
            {
                buildData.InstallSource = installSource;
                EditorUtility.SetDirty(buildData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Release Store set to: {installSource}");
            }
        }

        private static void WriteSegmentKeyToAnalyticsConfig(string segmentWriteKey)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AnalyticsConfiguration)}");

            switch (guids.Length)
            {
                case 0:
                    Debug.LogError($"{nameof(AnalyticsConfiguration)} asset not found!");
                    return;
                case > 1:
                    Debug.LogWarning($"Multiple {nameof(AnalyticsConfiguration)} assets found. Using the first one.");
                    break;
            }

            AssetDatabase.Refresh();
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            AnalyticsConfiguration config = AssetDatabase.LoadAssetAtPath<AnalyticsConfiguration>(assetPath);

            if (config != null)
            {
                Debug.Log($"[SEGMENT]: write key length {segmentWriteKey.Length}");

                config.SetWriteKey(segmentWriteKey);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[SEGMENT]: write key saved");
            }
            else // TODO (Vit): create default and add to Addressables (config = ScriptableObject.CreateInstance<AnalyticsConfiguration>());
                Debug.LogWarning($"{nameof(AnalyticsConfiguration)} asset not found , when trying to load it from AssetDatabase. Creating SO config file via {nameof(ScriptableObject.CreateInstance)}");
        }
    }
}
