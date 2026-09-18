using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
    /// <summary>Batchmode entrypoint for local Linux development builds.</summary>
    public static class LinuxPlayerBuild
    {
        public static void BuildDevelopment()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this method with -batchmode.");

            int exitCode = 1;

            try
            {
                string? output = Environment.GetEnvironmentVariable("DCL_BUILD_OUT");

                if (string.IsNullOrWhiteSpace(output))
                    throw new InvalidOperationException("Set DCL_BUILD_OUT to the output executable path.");

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                    throw new InvalidOperationException("Start Unity with -buildTarget StandaloneLinux64.");

                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                    throw new InvalidOperationException("Install Linux Build Support for this Unity editor.");

                var scenes = new List<string>();

                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                    if (scene.enabled)
                        scenes.Add(scene.path);

                if (scenes.Count == 0)
                    throw new InvalidOperationException("Enable at least one scene in Build Settings.");

                EditorApplication.ExecuteMenuItem("File/Save Project");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = Path.GetFullPath(output),
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.Development,
                });

                exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;

                if (exitCode != 0)
                    ReportHub.LogError(ReportCategory.ENGINE, $"Linux build: {report.summary.result}, {report.summary.totalErrors} errors.");
            }
            catch (Exception exception)
            {
                ReportHub.LogException(exception, ReportCategory.ENGINE);
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
