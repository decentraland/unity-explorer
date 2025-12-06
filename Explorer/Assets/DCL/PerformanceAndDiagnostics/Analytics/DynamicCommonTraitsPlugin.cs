using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using Newtonsoft.Json.Linq;
using System;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class DynamicCommonTraitsPlugin : IAnalyticsPlugin
    {
        private const string NOT_CONFIGURED = "NOT CONFIGURED";

        private readonly IRealmData realmData;
        private readonly IExposedTransform playerTransform;
        private readonly IWeb3IdentityCache? identityCache;

        public DynamicCommonTraitsPlugin(IRealmData realmData, IWeb3IdentityCache? identityCache, IExposedTransform playerTransform)
        {
            this.realmData = realmData;
            this.identityCache = identityCache;
            this.playerTransform = playerTransform;
        }

        public void Track(JObject trackEvent)
        {
            trackEvent["dcl_eth_address"] = identityCache?.Identity?.Address == null ? NOT_CONFIGURED : identityCache.Identity.Address.ToString();
            trackEvent["auth_chain"] = identityCache?.Identity?.AuthChain == null ? NOT_CONFIGURED : identityCache.Identity.AuthChain.ToString();
            trackEvent["realm"] = realmData is not { Configured: true } ? NOT_CONFIGURED : realmData.RealmName;
            trackEvent["realm_url"] = realmData is not { Configured: true } ? NOT_CONFIGURED : realmData.Ipfs.CatalystBaseUrl.Value;
            trackEvent["parcel"] = playerTransform == null ? NOT_CONFIGURED : playerTransform.Position.ToParcel().ToString();
            trackEvent["position"] = playerTransform == null ? NOT_CONFIGURED : playerTransform.Position.Value.ToShortString();
            trackEvent["direct"] = true;
        }
    }
}
