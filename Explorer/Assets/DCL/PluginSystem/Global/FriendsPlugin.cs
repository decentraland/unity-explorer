using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Friends;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connectivity;
using DCL.Profiles;
using DCL.Web3.Identities;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IOnlineUsersProvider apiOnlineUsersProvider;
        private readonly IRoomHub roomHub;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private RPCFriendsService? friendsService;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource,
            IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            FeatureFlagsCache featureFlagsCache,
            IOnlineUsersProvider apiOnlineUsersProvider,
            IRoomHub roomHub)
        {
            this.dclUrlSource = dclUrlSource;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.featureFlagsCache = featureFlagsCache;
            this.apiOnlineUsersProvider = apiOnlineUsersProvider;
            this.roomHub = roomHub;
        }

        public void Dispose()
        {
            lifeCycleCancellationToken.SafeCancelAndDispose();
            friendsService?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            var friendsCache = new FriendsCache();

            var archipelagoRealtime = new ArchipelagoRealtimeOnlineFriendsProvider(roomHub, friendEventBus, friendsCache);
            // Merge online users from archipelago api and the users provided by livekit
            var onlineUsersProvider = new CompositeOnlineFriendsProvider(this.apiOnlineUsersProvider,
                archipelagoRealtime);

            friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus, profileRepository, identityCache, onlineUsersProvider, friendsCache);

            if (featureFlagsCache.Configuration.IsEnabled("alpha-friends-enabled"))
            {
                archipelagoRealtime.SubscribeToRoomEvents();

                // Fire and forget as this task will never finish
                friendsService.SubscribeToIncomingFriendshipEventsAsync(
                                   CancellationTokenSource.CreateLinkedTokenSource(lifeCycleCancellationToken.Token, ct).Token)
                              .Forget();

                // It might be useful to fetch and fill the friends cache at start.
                // Otherwise we will not get online/offline realtime events from friends until we fetch it for the first time
            }

            // TODO: add the rest of the ui
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings { }
}
