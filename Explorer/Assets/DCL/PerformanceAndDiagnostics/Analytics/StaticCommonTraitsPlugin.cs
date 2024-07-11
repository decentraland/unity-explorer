using Segment.Analytics;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : EventPlugin
    {
        private readonly string dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly string sessionID = SystemInfo.deviceUniqueIdentifier + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        private readonly string rendererVersion = Application.version;
        private readonly string runtime = Application.isEditor? "editor" : Debug.isDebugBuild ? "debug" : "release";
        public override PluginType Type => PluginType.Enrichment;

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            trackEvent.Context["dcl_renderer_type"] = dclRendererType;
            trackEvent.Context["session_id"] = sessionID;
            trackEvent.Context["renderer_version"] = rendererVersion;
            trackEvent.Context["runtime"] = runtime;

            return trackEvent;
        }
    }
}
