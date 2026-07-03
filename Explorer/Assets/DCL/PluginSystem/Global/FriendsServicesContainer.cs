using DCL.FeatureFlags;
using DCL.Friends;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.SocialService;
using System;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Owns the friends data services so they exist before any consumer is constructed.
    ///     Created only when the FRIENDS feature is enabled; consumers receive nullable references.
    ///     The friends UI lives in <see cref="FriendsContainer" />, which builds on top of these services.
    /// </summary>
    public class FriendsServicesContainer : IDisposable
    {
        public RPCFriendsService RpcFriendsService { get; }
        public IFriendsService FriendsService { get; }
        public FriendsCache FriendsCache { get; }
        public FriendsConnectivityStatusTracker ConnectivityStatusTracker { get; }

        public FriendsServicesContainer(
            ISelfProfile selfProfile,
            IRPCSocialServices socialServicesRPC,
            IFriendsEventBus friendsEventBus,
            bool useAnalytics,
            IAnalyticsController? analyticsController)
        {
            FriendsCache = new FriendsCache();
            RpcFriendsService = new RPCFriendsService(friendsEventBus, FriendsCache, selfProfile, socialServicesRPC);
            FriendsService = useAnalytics ? new FriendServiceAnalyticsDecorator(RpcFriendsService, analyticsController!) : RpcFriendsService;
            ConnectivityStatusTracker = new FriendsConnectivityStatusTracker(friendsEventBus, FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS_CONNECTIVITY_STATUS));
        }

        public void Dispose()
        {
            ConnectivityStatusTracker.Dispose();
            FriendsCache.Clear();
            FriendsService.Dispose();
        }
    }
}
