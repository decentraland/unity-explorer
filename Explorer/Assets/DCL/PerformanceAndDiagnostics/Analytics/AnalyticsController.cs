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
        private readonly AnalyticsConfiguration configuration;
        private readonly IDictionary<string, JsonElement> commonParamsValues;

        private IRealmData realmData;
        private Transform playerTransform;
        private IWeb3IdentityCache identityCache;

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

        public AnalyticsController(IAnalyticsService analyticsService, AnalyticsConfiguration configuration)
        {
            analytics = analyticsService;
            this.configuration = configuration;

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
        }

        public void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, Transform playerTransform)
        {
            this.realmData = realmData;
            this.identityCache = identityCache;
            this.playerTransform = playerTransform;

            commonParamsValues["dcl_eth_address"] = identityCache?.Identity == null ? UNDEFINED : identityCache.Identity.Address.ToString();
            commonParamsValues["realm"] = realmData is not { Configured: true } ? UNDEFINED : realmData.RealmName;
            commonParamsValues["position"] = playerTransform == null ? UNDEFINED : playerTransform.position.ToShortString();

            // analytics.Identify();
        }

        public void Track(string eventName, Dictionary<string, JsonElement> properties = null)
        {
            if (configuration.EventIsEnabled(eventName))
                analytics.Track(eventName, properties.BuildWithPrefix(commonParams));
        }
    }

    public static class JsonObjectUtils
    {
        public static JsonObject BuildWithPrefix(this IDictionary<string, JsonElement> origin, IDictionary<string, JsonElement> prefix)
        {
            if (origin == null) return new JsonObject(prefix);

            foreach (KeyValuePair<string, JsonElement> element in origin)
                prefix.Add(element);

            var result = new JsonObject(prefix);

            foreach (KeyValuePair<string, JsonElement> element in origin)
                prefix.Remove(element);

            return result;
        }
    }
}
