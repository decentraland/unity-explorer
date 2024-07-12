
using DCL.PerformanceAndDiagnostics.Analytics;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [PublicAPI]
        public static Dictionary<string, object> Parameters { get; private set; }

        [UsedImplicitly]
        public static void PreExport()
        {
            Debug.Log("~~ PreExport ~~");

            // Get all environment variables
            var environmentVariables = Environment.GetEnvironmentVariables();
            Parameters = environmentVariables.Cast<DictionaryEntry>().ToDictionary(x => x.Key.ToString(), x => x.Value);

            // E.g. access like:
            // Debug.Log(Parameters["TEST_VALUE"] as string);

            //Unity suggestion: 1793168
            //This should ensure that the rosyln compiler has been ran and everything is generated as needed.
            EditorApplication.ExecuteMenuItem("File/Save Project");

            // Set version for this build
            var buildVersion = Parameters["BUILD_VERSION"] as string;
            PlayerSettings.bundleVersion = buildVersion;
            PlayerSettings.macOS.buildNumber = buildVersion;
            Debug.Log($"Build version set to: {buildVersion}");

            var segmentKey = Parameters["SEGMENT_WRITE_KEY"] as string;
            Debug.Log($"[SEGMENT]: write key found");
            WriteSegmentKeyToAnalyticsConfig(segmentKey);
        }

        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log("~~ PostExport ~~");
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

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            AnalyticsConfiguration config = AssetDatabase.LoadAssetAtPath<AnalyticsConfiguration>(assetPath);

            if (config == null)
            {
                Debug.LogWarning($"{nameof(AnalyticsConfiguration)} asset not found , when trying to load it from AssetDatabase. Creating SO config file via {nameof(ScriptableObject.CreateInstance)}");
                return;
                // TODO (Vit): create and add to Addressables
                config = ScriptableObject.CreateInstance<AnalyticsConfiguration>();
            }

            config.SetWriteKey(segmentWriteKey);
            AssetDatabase.SaveAssetIfDirty(config);
            EditorUtility.SetDirty(config);

            Debug.Log("[SEGMENT]: write key saved");
        }
    }
}
