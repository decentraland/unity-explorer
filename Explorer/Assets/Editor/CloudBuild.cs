using AltTester.AltTesterUnitySDK.Editor;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using Global.AppArgs;
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
        private static string DEEPLINK_WHITELISTED_WORLDS = "DEEPLINK_WHITELISTED_WORLDS";

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

            if (Parameters.TryGetValue(DEEPLINK_WHITELISTED_WORLDS, out object whitelistedWorlds))
            {
                Debug.Log("[DEEPLINK_WHITELISTED_WORLDS]: value found");
                WriteWhitelistedWorldsToConfig(whitelistedWorlds as string);
            }
            else
            {
                Debug.Log("[DEEPLINK_WHITELISTED_WORLDS]: value not found");
            }

            if (Parameters.TryGetValue("IS_RELEASE_BUILD", out object isReleaseBuild)
                && (isReleaseBuild as string) == "true")
            {
                Debug.Log("[ALTTESTER]: Release build — removing AltTester scripting define");
                AltBuilder.RemoveAltTesterFromScriptingDefineSymbols(BuildTargetGroup.Standalone);
            }

            DesktopStandaloneSettings.CopyPDBFiles = true;
        }

        // Defined in the @T_MacOS/@T_Windows64 configurations in Unity Cloud
        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log($"~~ {nameof(CloudBuild)} PostExport ~~");
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

        // Bakes the DEEPLINK_WHITELISTED_WORLDS secret (comma-separated world ENS names) into the
        // DeepLinkWorldWhitelist resource so the deep-link allowlist can trust those worlds' dev params at runtime.
        // The asset is created under Resources on first build so DeepLinkAllowlist can Resources.Load it at cold-start.
        private static void WriteWhitelistedWorldsToConfig(string csv)
        {
            var worlds = new List<string>();

            if (!string.IsNullOrWhiteSpace(csv))
                foreach (string world in csv.Split(','))
                {
                    string trimmed = world.Trim();

                    if (trimmed.Length > 0)
                        worlds.Add(trimmed);
                }

            const string RESOURCES_DIR = "Assets/Resources";
            string assetPath = $"{RESOURCES_DIR}/{DeepLinkWorldWhitelist.RESOURCE_NAME}.asset";

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(DeepLinkWorldWhitelist)}");
            DeepLinkWorldWhitelist config;

            if (guids.Length > 0)
                config = AssetDatabase.LoadAssetAtPath<DeepLinkWorldWhitelist>(AssetDatabase.GUIDToAssetPath(guids[0]));
            else
            {
                config = ScriptableObject.CreateInstance<DeepLinkWorldWhitelist>();

                if (!AssetDatabase.IsValidFolder(RESOURCES_DIR))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                AssetDatabase.CreateAsset(config, assetPath);
            }

            config.SetWorlds(worlds.ToArray());
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DEEPLINK_WHITELISTED_WORLDS]: baked {worlds.Count} world(s) into {assetPath}");
        }
    }
}
