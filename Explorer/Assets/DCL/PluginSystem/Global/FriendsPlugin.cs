using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.FriendPanel;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connectivity;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.UI.MainUI;
using MVC;
using System.Threading;
using Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class FriendsPlugin : IDCLGlobalPlugin<FriendsPluginSettings>
    {
        private readonly MainUIView mainUIView;
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileCache profileCache;
        private readonly IProfileRepository profileRepository;
        private readonly IOnlineUsersProvider apiOnlineUsersProvider;
        private readonly IRoomHub roomHub;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();

        private RPCFriendsService? friendsService;

        private FriendsPanelController? friendsPanelController;

        public FriendsPlugin(
            MainUIView mainUIView,
            IDecentralandUrlsSource dclUrlSource,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWeb3IdentityCache web3IdentityCache,
            IProfileCache profileCache,
            IProfileRepository profileRepository,
            IOnlineUsersProvider apiOnlineUsersProvider,
            IRoomHub roomHub)
        {
            this.mainUIView = mainUIView;
            this.dclUrlSource = dclUrlSource;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.web3IdentityCache = web3IdentityCache;
            this.profileCache = profileCache;
            this.profileRepository = profileRepository;
            this.apiOnlineUsersProvider = apiOnlineUsersProvider;
            this.roomHub = roomHub;
        }

        public void Dispose()
        {
            friendsPanelController?.Dispose();
            lifeCycleCancellationToken.SafeCancelAndDispose();
            friendsService?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(FriendsPluginSettings settings, CancellationToken ct)
        {
            IFriendsEventBus friendEventBus = new DefaultFriendsEventBus();

            var friendsCache = new FriendsCache();

            friendsService = new RPCFriendsService(URLAddress.FromString(dclUrlSource.Url(DecentralandUrl.ApiFriends)),
                friendEventBus, profileRepository, web3IdentityCache, friendsCache);

            // Fire and forget as this task will never finish
            friendsService.SubscribeToIncomingFriendshipEventsAsync(
                               CancellationTokenSource.CreateLinkedTokenSource(lifeCycleCancellationToken.Token, ct).Token)
                          .Forget();

            var persistentFriendsOpenerController = new PersistentFriendPanelOpenerController(() => mainUIView.SidebarView.PersistentFriendsPanelOpener, mvcManager);

            mvcManager.RegisterController(persistentFriendsOpenerController);

            FriendsPanelView friendsPanelPrefab = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.FriendsPanelPrefab, ct)).GetComponent<FriendsPanelView>();

            friendsPanelController = new FriendsPanelController(FriendsPanelController.CreateLazily(friendsPanelPrefab, null),
                mainUIView.ChatView,
                friendsService,
                friendEventBus,
                mvcManager,
                web3IdentityCache,
                profileCache,
                profileRepository);

            mvcManager.RegisterController(friendsPanelController);
        }
    }

    public class FriendsPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceGameObject FriendsPanelPrefab { get; set; } = null!;
    }
}
