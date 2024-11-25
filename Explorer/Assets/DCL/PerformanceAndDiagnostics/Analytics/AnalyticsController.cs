using DCL.Diagnostics;
using DCL.Web3.Identities;
using ECS;
using Global.AppArgs;
using Segment.Serialization;
using UnityEngine;
using Utility;
using static DCL.PerformanceAndDiagnostics.Analytics.IAnalyticsController;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsController : IAnalyticsController
    {
        private readonly IAnalyticsService analytics;

        private bool isInitialized;
        public AnalyticsConfiguration Configuration { get; }

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IAppArgs appArgs,
            AnalyticsConfiguration configuration,
            LauncherTraits launcherTraits,
            BuildData buildData)
        {
            analytics = analyticsService;
            Configuration = configuration;

            analytics.AddPlugin(new StaticCommonTraitsPlugin(appArgs, launcherTraits, buildData));
        }

        public void Initialize(IWeb3Identity? web3Identity)
        {
            TrackSystemInfo();
            analytics.Identify(web3Identity?.Address ?? "not cached");
            analytics.Flush();

            isInitialized = true;
        }

        public void SetCommonParam(IRealmData realmData, IWeb3IdentityCache? identityCache, IExposedTransform playerTransform)
        {
            analytics.AddPlugin(new DynamicCommonTraitsPlugin(realmData, identityCache, playerTransform));

            if (identityCache != null)
                Identify(identityCache.Identity);
        }

        public void Track(string eventName, JsonObject? properties = null)
        {
            if (!isInitialized)
                ReportHub.LogError(ReportCategory.ANALYTICS, $"Analytics {nameof(Track)} called before initialization. Event {eventName} won't be tracked.");

            if (Configuration.EventIsEnabled(eventName))
                analytics.Track(eventName, properties);
        }

        public void Identify(IWeb3Identity? identity)
        {
            if (identity != null)
            {
                analytics.Flush();

                analytics.Identify(identity.Address, new JsonObject
                    {
                        ["dcl_eth_address"] = identity.Address != null ? identity.Address.ToString() : UNDEFINED,
                        ["auth_chain"] = identity.AuthChain != null ? identity.AuthChain.ToString() : UNDEFINED,
                    }
                );

                analytics.Flush();
            }
        }

        private void TrackSystemInfo()
        {
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

        public void Flush() =>
            analytics.Flush();
    }
}
