using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IProfileRepository profileRepository;
        private IFriendsService? friendsService;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource,
            IProfileRepository profileRepository)
        {
            this.dclUrlSource = dclUrlSource;
            this.profileRepository = profileRepository;
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
                friendEventBus, profileRepository);

            PaginatedFriendsResult friendsResult = await friendsService.GetFriendsAsync(1, 10, ct);

            // TODO: add the rest of the ui
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
    }
}
