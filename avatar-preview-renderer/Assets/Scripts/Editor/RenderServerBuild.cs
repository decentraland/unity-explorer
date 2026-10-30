using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor
{
    /// <summary>
    /// Builds the native render server: a Linux player with DCL_RENDER_SERVER defined, rendering on
    /// Vulkan where there is a GPU and OpenGL (Mesa llvmpipe) on a CPU server. The WebGL build is
    /// untouched by it.
    /// </summary>
    /// <remarks>
    /// Batch mode: <c>Unity -batchmode -quit -projectPath avatar-preview-renderer
    /// -executeMethod Editor.RenderServerBuild.Build</c>. The output path defaults to
    /// Builds/RenderServer/renderer.x86_64 and can be moved with RENDER_SERVER_OUTPUT.
    /// </remarks>
    public static class RenderServerBuild
    {
        private const string DEFINE = "DCL_RENDER_SERVER";
        private const string DEFAULT_OUTPUT = "Builds/RenderServer/renderer.x86_64";

        [UsedImplicitly]
        [MenuItem("Decentraland/Build Render Server (Linux)")]
        public static void Build()
        {
            // The player rejects Mesa's CPU Vulkan device (lavapipe), so a CPU server runs on OpenGL
            // (llvmpipe); Vulkan stays first for machines with a GPU. ProjectSettings pins both, and
            // failing here catches OpenGL being dropped from the list.
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneLinux64);

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64)
                || Array.IndexOf(apis, GraphicsDeviceType.OpenGLCore) < 0)
            {
                Fail("Linux graphics APIs must include OpenGLCore (Player Settings > Linux > Other Settings)");
                return;
            }

            // Ahead-of-time code: Mono's JIT aborts under x86_64 emulation (Rosetta, QEMU), which is how
            // the image runs on ARM hosts.
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
            {
                Fail("The Standalone scripting backend must be IL2CPP (Player Settings > Linux > Other Settings)");
                return;
            }

            var output = Environment.GetEnvironmentVariable("RENDER_SERVER_OUTPUT");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
                    .ToArray(),
                locationPathName = string.IsNullOrEmpty(output) ? DEFAULT_OUTPUT : output,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                extraScriptingDefines = new[] { DEFINE },
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"Render server build {report.summary.result} with {report.summary.totalErrors} errors");
                return;
            }

            Debug.Log($"Render server built to {report.summary.outputPath}");
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);

            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
