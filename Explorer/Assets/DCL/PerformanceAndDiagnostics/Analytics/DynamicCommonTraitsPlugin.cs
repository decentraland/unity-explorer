﻿using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using Segment.Analytics;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class DynamicCommonTraitsPlugin : EventPlugin
    {
        private const string NOT_CONFIGURED = "NOT CONFIGURED";

        private readonly IRealmData realmData;
        private readonly IExposedTransform playerTransform;
        private readonly IWeb3IdentityCache? identityCache;
        public override PluginType Type => PluginType.Enrichment;

        public DynamicCommonTraitsPlugin(IRealmData realmData, IWeb3IdentityCache? identityCache, IExposedTransform playerTransform)
        {
            this.realmData = realmData;
            this.identityCache = identityCache;
            this.playerTransform = playerTransform;
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            trackEvent.Context["dcl_eth_address"] = identityCache?.Identity?.Address == null ? NOT_CONFIGURED : identityCache.Identity.Address.ToString();
            trackEvent.Context["auth_chain"] = identityCache?.Identity?.AuthChain == null? NOT_CONFIGURED : identityCache.Identity.AuthChain.ToString();
            trackEvent.Context["realm"] = realmData is not { Configured: true } ? NOT_CONFIGURED : realmData.RealmName;
            trackEvent.Context["parcel"] = playerTransform == null? NOT_CONFIGURED : playerTransform.Position.ToParcel().ToString();
            trackEvent.Context["position"] = playerTransform == null? NOT_CONFIGURED : playerTransform.Position.Value.ToShortString();
            trackEvent.Context["direct"] = true;

            return trackEvent;
        }
    }
}
