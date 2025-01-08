using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private IFriendsService? friendsService;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource,
            IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache)
        {
            this.dclUrlSource = dclUrlSource;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
        }

        public void Dispose()
        {
            friendsService?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus, profileRepository, identityCache);

            // TODO: add the rest of the ui
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
    }
}
