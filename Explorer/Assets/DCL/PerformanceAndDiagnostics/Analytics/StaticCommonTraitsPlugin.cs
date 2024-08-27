using Segment.Analytics;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : EventPlugin
    {
        private readonly string dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly string rendererVersion = Application.version;
        private readonly string runtime = Application.isEditor? "editor" : Debug.isDebugBuild ? "debug" : "release";
        private readonly string os = SystemInfo.operatingSystem;

        private readonly string sessionId;
        private readonly string launcherAnonymousId;

        public override PluginType Type => PluginType.Enrichment;

        public StaticCommonTraitsPlugin(LauncherTraits launcherTraits)
        {
            this.sessionId = !string.IsNullOrEmpty(launcherTraits.SessionId) ? launcherTraits.SessionId : SystemInfo.deviceUniqueIdentifier + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            this.launcherAnonymousId = launcherTraits.LauncherAnonymousId;
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
