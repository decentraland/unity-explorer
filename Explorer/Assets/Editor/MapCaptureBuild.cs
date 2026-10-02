using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Editor
{
    // Windows build of the MapCapture scene alone: `Unity.exe -batchmode -quit -executeMethod Editor.MapCaptureBuild.Build`.
    // Not part of the Cloud Build pipeline (see CloudBuild.cs).
    public static class MapCaptureBuild
    {
        private const string SCENE_PATH = "Assets/Scenes/MapCapture.unity";
        private const string OUTPUT_PATH = "Builds/MapCapture/MapCapture.exe";

        public static void Build()
        {
            // The project's default app icon (Assets/Textures/Icons/Logo.jpg) crashes Unity 6000.5.9f1's
            // batch-mode AddIconToWindowsExecutable step. Irrelevant for a capture build, so clear
            // it in-memory only (never saved back to ProjectSettings.asset) to skip that codepath entirely.
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[0]);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, new Texture2D[0]);

            AddressableAssetSettings.BuildPlayerContent();

            Debug.Log($"[MapCaptureBuild] Building {SCENE_PATH} to {OUTPUT_PATH}");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = OUTPUT_PATH,
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
