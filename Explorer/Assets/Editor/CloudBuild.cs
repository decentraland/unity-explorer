using AltTester.AltTesterUnitySDK.Editor;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.CrashReporting;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [PublicAPI]
        public static Dictionary<string, object> Parameters { get; private set; }

        private const string SEGMENT_WRITE_KEY = "SEGMENT_WRITE_KEY";

        private static readonly string[] DEBUG_SYMBOL_FOLDER_PATTERNS =
        {
            "*_BackUpThisFolder_ButDontShipItWithYourGame",
            "*_BurstDebugInformation_DoNotShip",
        };

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

            if (IsReleaseBuild())
            {
                Debug.Log("[ALTTESTER]: Release build — removing AltTester scripting define");
                AltBuilder.RemoveAltTesterFromScriptingDefineSymbols(BuildTargetGroup.Standalone);
            }
            else
            {
                // Skips the Cloud Diagnostics symbol generation and upload; Sentry symbolicates the other builds
                Debug.Log("[CLOUD_DIAGNOSTICS]: Not a release build — disabling crash reporting");
                CrashReportingSettings.enabled = false;
                // usymtool still uploads with crash reporting off as long as the editor process holds this token
                Environment.SetEnvironmentVariable("USYM_UPLOAD_AUTH_TOKEN", null);
            }

            DesktopStandaloneSettings.CopyPDBFiles = KeepsDebugSymbols();
        }

        // Defined in the @T_MacOS/@T_Windows64 configurations in Unity Cloud
        [UsedImplicitly]
        public static void PostExport(string exportPath)
        {
            Debug.Log($"~~ {nameof(CloudBuild)} PostExport ({exportPath}) ~~");

            if (!KeepsDebugSymbols())
                DeleteDebugSymbolFolders(exportPath);
        }

        // Read from the environment rather than Parameters: a domain reload between PreExport and PostExport resets static state
        private static bool IsReleaseBuild() =>
            Environment.GetEnvironmentVariable("IS_RELEASE_BUILD") == "true";

        // Release builds keep them for post-hoc symbolication, Sentry-enabled builds upload them to Sentry
        private static bool KeepsDebugSymbols() =>
            IsReleaseBuild() || Environment.GetEnvironmentVariable("SENTRY_ENABLED") == "true";

        private static void DeleteDebugSymbolFolders(string exportPath)
        {
            // Standalone builds put the debug folders next to the player: the .exe or .app path points one level too deep
            string playerPath = exportPath.TrimEnd('/', '\\');

            string buildFolder = Directory.Exists(playerPath) && !playerPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                ? playerPath
                : Path.GetDirectoryName(playerPath);

            if (string.IsNullOrEmpty(buildFolder) || !Directory.Exists(buildFolder))
            {
                Debug.LogWarning($"[DEBUG_SYMBOLS]: build folder not found for export path '{exportPath}'");
                return;
            }

            foreach (string pattern in DEBUG_SYMBOL_FOLDER_PATTERNS)
            foreach (string folder in Directory.GetDirectories(buildFolder, pattern, SearchOption.TopDirectoryOnly))
            {
                Directory.Delete(folder, true);
                Debug.Log($"[DEBUG_SYMBOLS]: deleted {folder}");
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
