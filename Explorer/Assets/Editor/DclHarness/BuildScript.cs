#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
        Debug.Log($"[BuildScript] START {DateTime.Now:HH:mm:ss} target={target} opts={extra}");
        try
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);

            var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
            PlayerSettings.bundleVersion = Env("DCL_BUILD_VERSION", "0.0.0-dev");

            ApplyGfxApi(target);

            string outPath = ResolveOutPath(target, artifact);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Env("DCL_MAIN_SCENE", "Assets/Scenes/Main.unity") },
                locationPathName = outPath,
                target = target,
                options = extra,
            };
            Debug.Log($"[BuildScript] BuildPlayer -> {outPath}");
            BuildSummary s = BuildPipeline.BuildPlayer(opts).summary;
            Debug.Log($"[BuildScript] RESULT={s.result} errors={s.totalErrors} sizeMB={s.totalSize / 1048576} secs={s.totalTime.TotalSeconds:F0}");
            EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[BuildScript] EXCEPTION " + e);
            EditorApplication.Exit(3);
        }
    }

    private static void ApplyGfxApi(BuildTarget target)
    {
        string api = Environment.GetEnvironmentVariable("DCL_GFX_API");
        if (string.IsNullOrEmpty(api)) return;
        UnityEngine.Rendering.GraphicsDeviceType t = api.ToLowerInvariant() switch
        {
            "vulkan" => UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
            "gl" or "glcore" => UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore,
            "d3d11" => UnityEngine.Rendering.GraphicsDeviceType.Direct3D11,
            "d3d12" => UnityEngine.Rendering.GraphicsDeviceType.Direct3D12,
            "metal" => UnityEngine.Rendering.GraphicsDeviceType.Metal,
            _ => throw new ArgumentException($"unknown DCL_GFX_API={api}"),
        };
        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(target, new[] { t });
        Debug.Log($"[BuildScript] forced graphics API {t} for {target}");
    }

    private static string ResolveOutPath(BuildTarget target, string artifact)
    {
        string custom = Environment.GetEnvironmentVariable("DCL_BUILD_OUT");
        if (!string.IsNullOrEmpty(custom)) return custom;
        string plat = target switch
        {
            BuildTarget.StandaloneWindows64 => "Windows",
            BuildTarget.StandaloneLinux64 => "Linux",
            BuildTarget.StandaloneOSX => "Mac",
            _ => target.ToString(),
        };

        string repo = System.IO.Directory.GetParent(Application.dataPath).Parent.FullName;
        return System.IO.Path.Combine(repo, "build", plat, artifact);
    }

    private static string Env(string k, string dflt)
    {
        string v = Environment.GetEnvironmentVariable(k);
        return string.IsNullOrEmpty(v) ? dflt : v;
    }
}
#endif
