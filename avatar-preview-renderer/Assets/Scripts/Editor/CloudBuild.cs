using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class CloudBuild
    {
        [UsedImplicitly]
        public static void PreExport()
        {
            // Get all environment variables
            var environmentVariables = Environment.GetEnvironmentVariables();

            // Set version for this build
            var buildVersion = environmentVariables["BUILD_VERSION"] as string;
            PlayerSettings.bundleVersion = buildVersion;
            Debug.Log($"Build version set to: {buildVersion}");
        }
    }
}