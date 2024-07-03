using DCL.Rendering.Menus;
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

            CompileSceneShader.CompileTheSceneShader(EditorUserBuildSettings.activeBuildTarget);

            // Set version for this build
            var buildVersion = Parameters["BUILD_VERSION"] as string;
            PlayerSettings.bundleVersion = buildVersion;
            PlayerSettings.macOS.buildNumber = buildVersion;
            Debug.Log($"Build version set to: {buildVersion}");
        }

        [UsedImplicitly]
        public static void PostExport()
        {
            Debug.Log("~~ PostExport ~~");
        }
    }
}
