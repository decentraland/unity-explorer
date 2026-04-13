using Global.AppArgs;
using Global.Versioning;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : IAnalyticsPlugin
    {
        private const string UNITY_EDITOR = "unity-editor";
        private const string DCL_EDITOR = "dcl-editor";
        private const string DEBUG = "debug";
        private const string RELEASE = "release";

        private readonly JToken sessionId;
        private readonly JToken launcherAnonymousId;
        private readonly string? campaignAnonUserId;

        private readonly JToken dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly JToken rendererVersion;
        private readonly JToken installSource;
        private readonly JToken os = SystemInfo.operatingSystem;
        private readonly JToken runtime;

        private readonly bool isLocalSceneDevelopment;

        public StaticCommonTraitsPlugin(IAppArgs appArgs, string sessionId, string launcherAnonymousId, string installSource, DCLVersion dclVersion, string? campaignAnonUserId = null)
        {
            this.sessionId = sessionId;
            this.launcherAnonymousId = launcherAnonymousId;
            this.campaignAnonUserId = campaignAnonUserId;

            runtime = ChooseRuntime(appArgs);
            this.installSource = installSource;
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

        public void Track(JObject trackEvent)
        {
            trackEvent["dcl_renderer_type"] = dclRendererType;
            trackEvent["session_id"] = sessionId;
            trackEvent["launcher_anonymous_id"] = launcherAnonymousId;
            trackEvent["renderer_version"] = rendererVersion;
            trackEvent["install_source"] = installSource;
            trackEvent["runtime"] = runtime;
            trackEvent["operating_system"] = os;
            trackEvent["is_local_scene"] = isLocalSceneDevelopment;

            if (!string.IsNullOrEmpty(campaignAnonUserId))
                trackEvent["campaignAnonUserId"] = campaignAnonUserId;
        }
    }

    public readonly struct LauncherTraits
    {
        public readonly string LauncherAnonymousId;
        public readonly string SessionId;
        public readonly string? CampaignAnonUserId;

        private LauncherTraits(string launcherAnonymousId, string sessionId, string? campaignAnonUserId)
        {
            LauncherAnonymousId = launcherAnonymousId;
            SessionId = sessionId;
            CampaignAnonUserId = campaignAnonUserId;
        }

        public static LauncherTraits FromAppArgs(IAppArgs appArgs)
        {
            appArgs.TryGetValue(AppArgsFlags.Analytics.LAUNCHER_ID, out string? launcherAnonymousId);
            appArgs.TryGetValue(AppArgsFlags.Analytics.SESSION_ID, out string? sessionId);
            appArgs.TryGetValue(AppArgsFlags.Analytics.CAMPAIGN_ID, out string? campaignAnonUserId);
            return new LauncherTraits(launcherAnonymousId!, sessionId!, campaignAnonUserId);
        }
    }
}
