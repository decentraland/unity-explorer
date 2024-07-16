using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS;
using Segment.Analytics;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
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
            trackEvent.Context["dcl_eth_address"] = identityCache?.Identity?.Address == null ? "NOT CONFIGURED" : identityCache.Identity.Address.ToString();
            trackEvent.Context["auth_chain"] = identityCache?.Identity?.AuthChain == null? "NOT CONFIGURED" : identityCache.Identity.AuthChain.ToString();
            trackEvent.Context["realm"] = realmData is not { Configured: true } ? "NOT CONFIGURED" : realmData.RealmName;
            trackEvent.Context["position"] = playerTransform == null? "NOT CONFIGURED" : playerTransform.Position.Value.ToShortString();

            return trackEvent;
        }
    }
}
