using DCL.Utilities.Extensions;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS;
using Newtonsoft.Json.Linq;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class DynamicCommonTraitsPlugin : IAnalyticsPlugin
    {
        private const string NOT_CONFIGURED = "NOT CONFIGURED";
        private const string GUEST_IDENTITY = "guest";
        private const string WEB2_IDENTITY = "web2";
        private const string WEB3_IDENTITY = "web3";

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
            trackEvent["identity_type"] = identityCache?.Identity == null
                ? NOT_CONFIGURED
                : identityCache.Identity.Method switch
                {
                    LoginMethod.GUEST => GUEST_IDENTITY,
                    LoginMethod.EMAIL_OTP or LoginMethod.GOOGLE or LoginMethod.DISCORD or LoginMethod.APPLE or LoginMethod.X => WEB2_IDENTITY,
                    LoginMethod.METAMASK or LoginMethod.WALLETCONNECT or LoginMethod.COINBASE or LoginMethod.FORTMATIC => WEB3_IDENTITY,
                    _ => NOT_CONFIGURED,
                };
            trackEvent["identity_method"] = identityCache?.Identity == null ? NOT_CONFIGURED : identityCache.Identity.Method.ToString();
            trackEvent["realm"] = realmData is not { Configured: true } ? NOT_CONFIGURED : realmData.RealmName;
            trackEvent["realm_url"] = realmData is not { Configured: true } ? NOT_CONFIGURED : realmData.Ipfs.CatalystBaseUrl.Value;
            trackEvent["parcel"] = playerTransform == null ? NOT_CONFIGURED : playerTransform.Position.ToParcel().ToString();
            trackEvent["position"] = playerTransform == null ? NOT_CONFIGURED : playerTransform.Position.Value.ToShortString();
            trackEvent["direct"] = true;
        }
    }
}
