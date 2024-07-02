using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using Segment.Analytics;
using Segment.Serialization;
using System;
using UnityEngine;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class StaticCommonTraitsPlugin : EventPlugin
    {
        private readonly string dclRendererType = SystemInfo.deviceType.ToString(); // Desktop, Console, Handeheld (Mobile), Unknown
        private readonly string sessionID = SystemInfo.deviceUniqueIdentifier + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        private readonly string rendererVersion = Application.version;
        private readonly string runtime = Application.isEditor ? "editor" : "build";
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

    public class DynamicCommonTraitsPlugin : EventPlugin
    {
        private readonly IRealmData realmData;
        private readonly ExposedTransform playerTransform;
        private readonly IWeb3IdentityCache identityCache;
        public override PluginType Type => PluginType.Enrichment;

        public DynamicCommonTraitsPlugin(IRealmData realmData, IWeb3IdentityCache identityCache, ExposedTransform playerTransform)
        {
            this.realmData = realmData;
            this.identityCache = identityCache;
            this.playerTransform = playerTransform;
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            trackEvent.Context["dcl_eth_address"] = identityCache!.Identity!.Address.ToString();
            trackEvent.Context["auth_chain"] = identityCache.Identity.AuthChain.ToString();
            trackEvent.Context["realm"] = realmData is not { Configured: true } ? "NOT CONFIGURED" : realmData.RealmName;
            trackEvent.Context["position"] = playerTransform.Position.Value.ToShortString();

            return trackEvent;
        }
    }

    public class AnalyticsController
    {
        private readonly IAnalyticsService analytics;
        private readonly AnalyticsConfiguration configuration;

        public AnalyticsController(IAnalyticsService analyticsService, AnalyticsConfiguration configuration)
        {
            analytics = analyticsService;
            this.configuration = configuration;

            analytics.AddPlugin(new StaticCommonTraitsPlugin());
        }

        public void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, ExposedTransform playerTransform)
        {
            analytics.Identify(identityCache.Identity.Address, new JsonObject
                {
                    ["dcl_eth_address"] = identityCache.Identity.Address.ToString(),
                    ["auth_chain"] = identityCache.Identity.AuthChain.ToString(),
                }
            );

            analytics.AddPlugin(new DynamicCommonTraitsPlugin(realmData, identityCache, playerTransform));
        }

        public void Track(string eventName, JsonObject properties = null)
        {
            if (configuration.EventIsEnabled(eventName))
                analytics.Track(eventName, properties);
        }
    }
}
