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

    public interface IAnalyticsController
    {
        AnalyticsConfiguration Configuration { get; }

        void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, ExposedTransform playerTransform);
        void Track(string eventName, JsonObject properties = null);
    }

    public class AnalyticsController : IAnalyticsController
    {
        private readonly IAnalyticsService analytics;
        public AnalyticsConfiguration Configuration { get; }

        public AnalyticsController(IAnalyticsService analyticsService, AnalyticsConfiguration configuration)
        {
            analytics = analyticsService;
            Configuration = configuration;

            analytics.AddPlugin(new StaticCommonTraitsPlugin());

            analytics.Track(AnalyticsEvents.General.SYSTEM_INFO_REPORT, new JsonObject
            {
                ["device_model"] = SystemInfo.deviceModel, // "XPS 17 9720 (Dell Inc.)"
                ["operating_system"] = SystemInfo.operatingSystem, // "Windows 11  (10.0.22631) 64bit"
                ["system_memory_size"] = SystemInfo.systemMemorySize, // 65220 in [MB]

                ["processor_type"] = SystemInfo.processorType, // "12th Gen Intel(R) Core(TM) i7-12700H"
                ["processor_count"] = SystemInfo.processorCount, // 20

                ["graphics_device_name"] = SystemInfo.graphicsDeviceName, // "NVIDIA GeForce RTX 3050 Laptop GPU"
                ["graphics_memory_size"] = SystemInfo.graphicsMemorySize, // 3965 in [MB]
                ["graphics_device_type"] = SystemInfo.graphicsDeviceType.ToString(), // "Direct3D11", Vulkan, OpenGLCore, XBoxOne...
                ["graphics_device_version"] = SystemInfo.graphicsDeviceVersion, // "Direct3D 11.0 [level 11.1]"
            });
        }

        public void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, ExposedTransform playerTransform)
        {
            analytics.AddPlugin(new DynamicCommonTraitsPlugin(realmData, identityCache, playerTransform));

            analytics.Identify(identityCache.Identity.Address, new JsonObject
                {
                    ["dcl_eth_address"] = identityCache.Identity.Address.ToString(),
                    ["auth_chain"] = identityCache.Identity.AuthChain.ToString(),
                }
            );
        }

        public void Track(string eventName, JsonObject properties = null)
        {
            if (Configuration.EventIsEnabled(eventName))
                analytics.Track(eventName, properties);
        }
    }
}
