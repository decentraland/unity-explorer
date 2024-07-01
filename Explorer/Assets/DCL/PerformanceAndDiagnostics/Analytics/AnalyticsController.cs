using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using Segment.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsController
    {
        private const string UNDEFINED = "undefined";

        private readonly IAnalyticsService analytics;
        private readonly IDictionary<string, JsonElement> commonParamsValues;

        private  IRealmData realmData;
        private  Transform playerTransform;
        private  IWeb3IdentityCache identityCache;

        private IDictionary<string, JsonElement> commonParams
        {
            get
            {
                commonParamsValues["dcl_eth_address"] = identityCache?.Identity == null ? UNDEFINED : identityCache.Identity.Address.ToString();
                commonParamsValues["realm"] = realmData is not { Configured: true } ? UNDEFINED : realmData.RealmName;
                commonParamsValues["position"] = playerTransform == null ? UNDEFINED : playerTransform.position.ToShortString();

                return commonParamsValues;
            }
        }

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            this.analytics = analyticsService;

            commonParamsValues = new Dictionary<string, JsonElement>
            {
                // Dynamic common parameters, updated on call
                ["dcl_eth_address"] = UNDEFINED,
                ["realm"] = UNDEFINED,
                ["position"] = UNDEFINED,
                // Static common parameters
                ["dcl_renderer_type"] = SystemInfo.deviceType.ToString(), // Desktop, Console, Handeheld (Mobile), Unknown
                ["session_id"] = SystemInfo.deviceUniqueIdentifier + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                ["renderer_version"] = Application.version,
                ["runtime"] = Application.isEditor ? "editor" : "build", // do we need it❓
            };

            SendSystemInfo();
        }

        public void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, Transform playerTransform)
        {
            this.realmData = realmData;

            this.identityCache = identityCache;
            this.playerTransform = playerTransform;
        }

        public void Track(string eventName, Dictionary<string, JsonElement> properties = null)
        {
            analytics.Track(eventName, properties.BuildWithPrefix(commonParams));
        }

        private void SendSystemInfo()
        {
            Track("system_info_report", new Dictionary<string, JsonElement>
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
    }

    public static class JsonObjectUtils
    {
        public static JsonObject BuildWithPrefix(this IDictionary<string, JsonElement> origin, IDictionary<string, JsonElement> prefix)
        {
            if (origin == null) return new JsonObject(prefix);

            foreach (KeyValuePair<string,JsonElement> element in origin)
                prefix.Add(element);

            var result = new JsonObject(prefix);

            foreach (KeyValuePair<string,JsonElement> element in origin)
                prefix.Remove(element);

            return result;
        }
    }
}
