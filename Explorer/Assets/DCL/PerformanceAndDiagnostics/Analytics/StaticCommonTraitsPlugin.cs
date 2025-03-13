using Cysharp.Threading.Tasks;
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
        private const string DEBUG = "debug";
        private const string RELEASE = "release";

        private readonly JsonElement sessionId;
        private readonly JsonElement launcherAnonymousId;

        private readonly JsonElement dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly JsonElement rendererVersion;
        private readonly JsonElement installSource;
        private readonly JsonElement os = SystemInfo.operatingSystem;
        private readonly JsonElement runtime;
        private readonly JsonElement localIPAddress;

        private JsonElement publicIPAddress;

        public override PluginType Type => PluginType.Enrichment;

        public StaticCommonTraitsPlugin(IAppArgs appArgs, UserIPAddressService userIPAddressService, string sessionId, string launcherAnonymousId, BuildData buildData, DCLVersion dclVersion)
        {
            this.sessionId = sessionId;
            this.launcherAnonymousId = launcherAnonymousId;

            runtime = ChooseRuntime(appArgs);
            installSource = buildData.InstallSource;
            rendererVersion = dclVersion.Version;

            localIPAddress = userIPAddressService.LocalIP;
            publicIPAddress = string.Empty;
            GetPublicIPAddressAsync(userIPAddressService).Forget();

            return;
            async UniTask GetPublicIPAddressAsync(UserIPAddressService ipAddressService) =>
                publicIPAddress = await ipAddressService.GetPublicIPAddressAsync();
        }

        private static string ChooseRuntime(IAppArgs appArgs)
        {
            if (Application.isEditor)
                return UNITY_EDITOR;

            if (appArgs.HasFlag(AppArgsFlags.DCL_EDITOR))
                return AppArgsFlags.DCL_EDITOR;

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
            trackEvent.Context["local_ip_address"] = localIPAddress;
            trackEvent.Context["public_ip_address"] = publicIPAddress;

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
