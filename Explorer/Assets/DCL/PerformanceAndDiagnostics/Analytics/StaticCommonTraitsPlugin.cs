using Global.AppArgs;
using Segment.Analytics;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : EventPlugin
    {
        private const string DCL_EDITOR = "dcl-editor";

        private readonly JsonElement sessionId;
        private readonly JsonElement launcherAnonymousId;

        private readonly JsonElement dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly JsonElement rendererVersion = Application.version;
        private readonly JsonElement os = SystemInfo.operatingSystem;
        private readonly JsonElement runtime;

        public override PluginType Type => PluginType.Enrichment;

        public StaticCommonTraitsPlugin(IAppArgs appArgs, LauncherTraits launcherTraits)
        {
            sessionId = !string.IsNullOrEmpty(launcherTraits.SessionId) ? launcherTraits.SessionId : SystemInfo.deviceUniqueIdentifier + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            launcherAnonymousId = launcherTraits.LauncherAnonymousId;

            runtime = ChooseRuntime(appArgs);
        }

        private static string ChooseRuntime(IAppArgs appArgs)
        {
            if (Application.isEditor)
                return "unity-editor";

            if (appArgs.HasFlag(DCL_EDITOR))
                return DCL_EDITOR;

            if (Debug.isDebugBuild || appArgs.HasDebugFlag())
                return "debug";

            return "release";
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            trackEvent.Context["dcl_renderer_type"] = dclRendererType;
            trackEvent.Context["session_id"] = sessionId;
            trackEvent.Context["launcher_anonymous_id"] = launcherAnonymousId;
            trackEvent.Context["renderer_version"] = rendererVersion;
            trackEvent.Context["runtime"] = runtime;
            trackEvent.Context["operating_system"] = os;

            return trackEvent;
        }
    }

    public struct LauncherTraits
    {
        public string LauncherAnonymousId;
        public string SessionId;
    }
}
