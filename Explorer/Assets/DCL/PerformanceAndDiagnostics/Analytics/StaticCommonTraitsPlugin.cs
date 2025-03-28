using Global.AppArgs;
using Global.Versioning;
using Segment.Analytics;
using Segment.Serialization;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : EventPlugin
    {
        private const string UNITY_EDITOR = "unity-editor";
        private const string DCL_EDITOR = "dcl-editor";
        private const string DEBUG = "debug";
        private const string RELEASE = "release";

        private readonly JsonElement sessionId;
        private readonly JsonElement launcherAnonymousId;

        private readonly JsonElement dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly JsonElement rendererVersion;
        private readonly JsonElement installSource;
        private readonly JsonElement os = SystemInfo.operatingSystem;
        private readonly JsonElement runtime;

        private readonly bool isLocalSceneDevelopment;

        public override PluginType Type => PluginType.Enrichment;

        public StaticCommonTraitsPlugin(IAppArgs appArgs, string sessionId, string launcherAnonymousId, BuildData buildData, DCLVersion dclVersion)
        {
            this.sessionId = sessionId;
            this.launcherAnonymousId = launcherAnonymousId;

            runtime = ChooseRuntime(appArgs);
            installSource = buildData.InstallSource;
            rendererVersion = dclVersion.Version;
            isLocalSceneDevelopment = appArgs.HasFlag(AppArgsFlags.LOCAL_SCENE);
        }

        private static string ChooseRuntime(IAppArgs appArgs)
        {
            if (Application.isEditor)
                return UNITY_EDITOR;

            if (appArgs.HasFlagWithValueTrue(AppArgsFlags.DCL_EDITOR))
                return DCL_EDITOR; // We send "dcl-editor" instead of "hub" to track it better on the analytics data side

            if (appArgs.HasDebugFlag())
                return DEBUG;

            return RELEASE;
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            trackEvent.Context["dcl_renderer_type"] = dclRendererType;
            trackEvent.Context["session_id"] = sessionId;
            trackEvent.Context["launcher_anonymous_id"] = launcherAnonymousId;
            trackEvent.Context["renderer_version"] = rendererVersion;
            trackEvent.Context["install_source"] = installSource;
            trackEvent.Context["runtime"] = runtime;
            trackEvent.Context["operating_system"] = os;
            trackEvent.Context["is_local_scene"] = isLocalSceneDevelopment;

            return trackEvent;
        }
    }

    public readonly struct LauncherTraits
    {
        public readonly string LauncherAnonymousId;
        public readonly string SessionId;

        private LauncherTraits(string launcherAnonymousId, string sessionId)
        {
            LauncherAnonymousId = launcherAnonymousId;
            SessionId = sessionId;
        }

        public static LauncherTraits FromAppArgs(IAppArgs appArgs)
        {
            appArgs.TryGetValue(AppArgsFlags.Analytics.LAUNCHER_ID, out string? launcherAnonymousId);
            appArgs.TryGetValue(AppArgsFlags.Analytics.SESSION_ID, out string? sessionId);
            return new LauncherTraits(launcherAnonymousId!, sessionId!);
        }
    }
}
