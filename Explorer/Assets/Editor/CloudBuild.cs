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

            if (Parameters.TryGetValue("IS_RELEASE_BUILD", out object isReleaseBuild)
                && (isReleaseBuild as string) == "true")
            {
                Debug.Log("[ALTTESTER]: Release build — removing AltTester scripting define");
                AltBuilder.RemoveAltTesterFromScriptingDefineSymbols(BuildTargetGroup.Standalone);
            }

            // CI-only escape hatch for hosts with a broken audio HAL.
            //
            // On GH-hosted macos-14 paravirt VMs (Apple Virtualization Framework
            // nested under their cloud hypervisor) the AppleVirtualSoundDevice
            // never replies in AudioDeviceCreateIOProcID / AudioObjectHasProperty
            // mach_msg calls. rust_audio_input_device_names → cpal::supports_input
            // iterates every HAL device and wedges on the very first one, blocking
            // the Unity main thread for the entire bootstrap (we observed the
            // freeze in three back-to-back sample(1) thread dumps on the
            // explorer-automation runner-validation workflow, all parked in
            // semaphore_wait_trap under HALC_ProxyIOContext::TellServerAboutStreamUsage).
            //
            // NO_LIVEKIT_MODE short-circuits both RustAudioClient.Init() and the
            // LiveKit branches of MultiplayerPlugin. We therefore enable it ONLY
            // for CI smoke builds via DCL_CI_NO_LIVEKIT=1 — production builds
            // (where this env var is unset) keep multiplayer comms / scene chat /
            // presence fully functional. CI smoke runs solo (no other players,
            // alfa-voice-chat feature flag off) so losing the LiveKit transport
            // there has no observable test impact.
            //
            // Remove this block once a runtime gate lands upstream in
            // decentraland/client-sdk-unity RustAudioClient.Init that can disable
            // only the rust_audio bring-up without touching LiveKit transport.
            if (Parameters.TryGetValue("DCL_CI_NO_LIVEKIT", out object noLivekit)
                && (noLivekit as string) == "1")
            {
                Debug.Log("[CI]: DCL_CI_NO_LIVEKIT=1 — adding NO_LIVEKIT_MODE to Standalone scripting defines");
                AddScriptingDefine(BuildTargetGroup.Standalone, "NO_LIVEKIT_MODE");
            }

            DesktopStandaloneSettings.CopyPDBFiles = true;
        }

        private static void AddScriptingDefine(BuildTargetGroup group, string define)
        {
            PlayerSettings.GetScriptingDefineSymbolsForGroup(group, out string[] defines);
            if (defines.Contains(define))
            {
                Debug.Log($"[CI]: {define} already present in {group} defines — no-op");
                return;
            }
            var newDefines = string.Join(";", defines.Append(define));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
            Debug.Log($"[CI]: {group} scripting defines now: {newDefines}");
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
    }
}
