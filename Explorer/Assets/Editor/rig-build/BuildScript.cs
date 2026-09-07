using AltTester.AltTesterUnitySDK.Editor;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor
{
    // Local batchmode build entry points — one -executeMethod target per platform,
    // e.g. -buildTarget StandaloneLinux64 -executeMethod Editor.BuildScript.BuildLinux64Dev.
    // The -buildTarget CLI argument is REQUIRED to match the method: switching the
    // active target from inside -executeMethod can tear down the script domain
    // mid-method, so this script only verifies the target and refuses on mismatch.
    // Releases go through Unity Cloud Build (CloudBuild.PreExport/PostExport); this
    // driver mirrors the PreExport steps that affect the produced player (Save
    // Project for Roslyn generation, AltTester define stripping on non-development
    // builds, PDB copying, bundle version) but not the Cloud-only asset wiring
    // (Segment write key, install source). Driven by environment variables:
    //
    //   DCL_BUILD_OUT      output path (default: <repo>/build/<platform>/<exe>)
    //   DCL_BUILD_VERSION  PlayerSettings.bundleVersion (default: 0.0.0-dev)
    //   DCL_GFX_API        force a graphics API: vulkan|gl|d3d11|d3d12|metal (optional)
    //   DCL_BUILD_BACKEND  il2cpp (default, matches the shipped client) | mono (fast, JIT —
    //                      boot/perf numbers do NOT transfer to the shipped client)
    //
    // All mutated PlayerSettings (backend, version, defines, graphics APIs) are
    // restored before exit so a build never leaves ProjectSettings.asset dirty.
    //
    // Exit codes: 0 success, 1 build failed, 3 exception, 4 active target mismatch
    // (pass -buildTarget / install the platform module) — so CI can branch on them.
    //
    // YES, LOCAL PLAYER BUILDS WORK. The upstream repo commits no build path — no
    // BuildPipeline.BuildPlayer, no build MenuItem — because releases go through
    // Unity Cloud Build. That is NOT the same as "impossible locally": this file
    // is the missing piece. Do not re-derive that conclusion.
    public static class BuildScript
    {
        public static void BuildWindows64()        => Run(BuildTarget.StandaloneWindows64, BuildOptions.None,        "Decentraland.exe");
        public static void BuildWindows64Dev()     => Run(BuildTarget.StandaloneWindows64, BuildOptions.Development, "Decentraland.exe");
        public static void BuildLinux64()          => Run(BuildTarget.StandaloneLinux64,   BuildOptions.None,        "decentraland-explorer.x86_64");
        public static void BuildLinux64Dev()       => Run(BuildTarget.StandaloneLinux64,   BuildOptions.Development, "decentraland-explorer.x86_64");
        public static void BuildMacUniversal()     => Run(BuildTarget.StandaloneOSX,       BuildOptions.None,        "Decentraland.app");
        public static void BuildMacUniversalDev()  => Run(BuildTarget.StandaloneOSX,       BuildOptions.Development, "Decentraland.app");

        private static void Run(BuildTarget target, BuildOptions extra, string artifact)
        {
            Debug.Log($"[BuildScript] START target={target} opts={extra}");
            int exitCode;

            try { exitCode = BuildOnce(target, extra, artifact); }
            catch (Exception e)
            {
                Debug.LogError("[BuildScript] EXCEPTION " + e);
                exitCode = 3;
            }

            // EditorApplication.Exit kills the process without unwinding, so it must
            // only run after BuildOnce has restored the mutated project settings.
            EditorApplication.Exit(exitCode);
        }

        private static int BuildOnce(BuildTarget target, BuildOptions extra, string artifact)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);

            // Guard, not a switch: AddressablesPlayerBuildProcessor builds content for
            // the ACTIVE target, so it must already be correct when BuildPlayer runs —
            // and switching from inside -executeMethod can abort this method via domain
            // reload. The caller selects the target with the -buildTarget CLI argument.
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.LogError($"[BuildScript] active target is {EditorUserBuildSettings.activeBuildTarget}, expected {target}. " +
                               $"Pass -buildTarget {target} on the Unity command line (and ensure the platform module is installed).");
                return 4;
            }

            // Unity suggestion 1793168 (same as CloudBuild.PreExport): ensures the
            // Roslyn source generators have run before the build. Runs before the
            // settings snapshot so it cannot persist any of the mutations below.
            EditorApplication.ExecuteMenuItem("File/Save Project");

            var named = NamedBuildTarget.FromBuildTargetGroup(group);

            ScriptingImplementation prevBackend = PlayerSettings.GetScriptingBackend(named);
            string prevVersion = PlayerSettings.bundleVersion;
            string prevMacBuildNumber = PlayerSettings.macOS.buildNumber;
            string prevDefines = PlayerSettings.GetScriptingDefineSymbols(named);
            bool prevUseDefaultGfx = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            GraphicsDeviceType[] prevGfxApis = PlayerSettings.GetGraphicsAPIs(target);

            try
            {
                // IL2CPP is the default because it is the shipped configuration and this
                // branch measures managed-code cost (boot time, allocation, GC) — Mono's
                // JIT-heavy startup does not transfer to the real client. Set
                // DCL_BUILD_BACKEND=mono for fast functional builds where timing is irrelevant.
                bool il2cpp = Env("DCL_BUILD_BACKEND", "il2cpp").ToLowerInvariant() != "mono";
                PlayerSettings.SetScriptingBackend(named, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
                Debug.Log($"[BuildScript] scripting backend={(il2cpp ? "IL2CPP" : "Mono2x")}");

                string version = Env("DCL_BUILD_VERSION", "0.0.0-dev");
                PlayerSettings.bundleVersion = version;
                PlayerSettings.macOS.buildNumber = version;

                // Same contract as CloudBuild.PreExport's IS_RELEASE_BUILD branch: a
                // non-development player must not ship the AltTester instrumentation server.
                bool release = (extra & BuildOptions.Development) == 0;

                if (release)
                {
                    Debug.Log("[BuildScript] release build — removing AltTester scripting define");
                    AltBuilder.RemoveAltTesterFromScriptingDefineSymbols(BuildTargetGroup.Standalone);
                }

                DesktopStandaloneSettings.CopyPDBFiles = true;

                // Without this the .app inherits whatever architecture the building machine
                // last had persisted, silently producing a single-arch "universal" artifact.
                // String-based settings API so this compiles without the Mac build module.
                if (target == BuildTarget.StandaloneOSX)
                    EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");

                // Optional graphics-API override. By default Unity auto-selects the
                // platform native API (D3D11 on Windows, Metal on macOS, Vulkan on
                // Linux); set DCL_GFX_API to pin one — handy for reproducing a
                // renderer-specific bug.
                ApplyGfxApi(target);

                string outPath = ResolveOutPath(target, artifact);
                string outDir = System.IO.Path.GetDirectoryName(outPath);

                if (!string.IsNullOrEmpty(outDir))
                    System.IO.Directory.CreateDirectory(outDir);

                var opts = new BuildPlayerOptions
                {
                    scenes = ResolveScenes(),
                    locationPathName = outPath,
                    target = target,
                    options = extra,
                };

                Debug.Log($"[BuildScript] BuildPlayer -> {outPath}");
                BuildReport report = BuildPipeline.BuildPlayer(opts);
                BuildSummary s = report.summary;
                Debug.Log($"[BuildScript] RESULT={s.result} errors={s.totalErrors} sizeMB={s.totalSize / 1048576} secs={s.totalTime.TotalSeconds:F0}");
                DumpErrorMessages(report);
                return s.result == BuildResult.Succeeded ? 0 : 1;
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(named, prevBackend);
                PlayerSettings.bundleVersion = prevVersion;
                PlayerSettings.macOS.buildNumber = prevMacBuildNumber;
                PlayerSettings.SetScriptingDefineSymbols(named, prevDefines);
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, prevUseDefaultGfx);
                PlayerSettings.SetGraphicsAPIs(target, prevGfxApis);
                AssetDatabase.SaveAssets();
                Debug.Log("[BuildScript] restored PlayerSettings (backend/version/defines/gfx)");
            }
        }

        // summary.totalErrors counts BuildReport messages that are never echoed to the log
        // (e.g. shader errors recorded by build steps), so a "Succeeded errors=N" line is
        // undiagnosable without this: print every Error/Exception/Assert message per step.
        private static void DumpErrorMessages(BuildReport report)
        {
            var printed = 0;

            foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage msg in step.messages)
            {
                if (msg.type != LogType.Error && msg.type != LogType.Exception && msg.type != LogType.Assert)
                    continue;

                Debug.Log($"[BuildScript] REPORT-ERROR step='{step.name}' {msg.type}: {msg.content}");

                if (++printed >= 60)
                {
                    Debug.Log("[BuildScript] REPORT-ERROR output truncated at 60 messages");
                    return;
                }
            }
        }

        private static void ApplyGfxApi(BuildTarget target)
        {
            string api = Environment.GetEnvironmentVariable("DCL_GFX_API");
            if (string.IsNullOrEmpty(api)) return;

            GraphicsDeviceType t = api.ToLowerInvariant() switch
            {
                "vulkan" => GraphicsDeviceType.Vulkan,
                "gl" or "glcore" => GraphicsDeviceType.OpenGLCore,
                "d3d11" => GraphicsDeviceType.Direct3D11,
                "d3d12" => GraphicsDeviceType.Direct3D12,
                "metal" => GraphicsDeviceType.Metal,
                _ => throw new ArgumentException($"unknown DCL_GFX_API={api}"),
            };

            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { t });
            Debug.Log($"[BuildScript] forced graphics API {t} for {target}");
        }

        private static string[] ResolveScenes()
        {
            // DCL_MAIN_SCENE is an explicit override; the project's build scene list
            // (EditorBuildSettings) stays the single source of truth otherwise.
            string overrideScene = Environment.GetEnvironmentVariable("DCL_MAIN_SCENE");

            if (!string.IsNullOrEmpty(overrideScene))
                return new[] { overrideScene };

            return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        }

        private static string ResolveOutPath(BuildTarget target, string artifact)
        {
            string custom = Environment.GetEnvironmentVariable("DCL_BUILD_OUT");
            if (!string.IsNullOrEmpty(custom)) return System.IO.Path.GetFullPath(custom);

            string plat = target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneLinux64 => "Linux",
                BuildTarget.StandaloneOSX => "Mac",
                _ => target.ToString(),
            };

            // <repo>/build/<Platform>/<artifact>   (repo = parent of Assets/..)
            string repo = System.IO.Directory.GetParent(Application.dataPath)!.Parent!.FullName;
            return System.IO.Path.Combine(repo, "build", plat, artifact);
        }

        private static string Env(string k, string dflt)
        {
            string v = Environment.GetEnvironmentVariable(k);
            return string.IsNullOrEmpty(v) ? dflt : v;
        }
    }
}
