using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    // Minimal local build for the map-capture pipeline (headless MCP-driven screenshots).
    // Not part of the Cloud Build pipeline (see CloudBuild.cs) — just a plain BuildPipeline.BuildPlayer
    // invocation so this can run via `Unity.exe -batchmode -quit -executeMethod Editor.MapCaptureBuild.Build`.
    public static class MapCaptureBuild
    {
        public static void Build()
        {
            string outputPath = "Builds/Windows/Explorer.exe";

            string[] scenes = EditorBuildSettings.scenes
                                                  .Where(s => s.enabled)
                                                  .Select(s => s.path)
                                                  .ToArray();

            // The project's default app icon (Assets/Textures/Icons/Logo.jpg) crashes Unity 6000.5.9f1's
            // batch-mode AddIconToWindowsExecutable step. Irrelevant for a headless capture build, so clear
            // it in-memory only (never saved back to ProjectSettings.asset) to skip that codepath entirely.
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[0]);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, new Texture2D[0]);

            Debug.Log($"[MapCaptureBuild] Building {scenes.Length} scene(s) to {outputPath}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache,
            };

            var report = BuildPipeline.BuildPlayer(options);

            Debug.Log($"[MapCaptureBuild] Result: {report.summary.result}, size={report.summary.totalSize} bytes, errors={report.summary.totalErrors}");

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
