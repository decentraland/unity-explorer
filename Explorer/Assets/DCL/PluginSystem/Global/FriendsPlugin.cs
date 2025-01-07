using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly IDecentralandUrlsSource dclUrlSource;

        public FriendsPlugin(IDecentralandUrlsSource dclUrlSource)
        {
            this.dclUrlSource = dclUrlSource;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            IFriendsService friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus);

            // TODO: add the rest of the ui
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
    }
}
