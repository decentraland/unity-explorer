using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.RealmNavigation;
using DCL.Settings.Settings;
using DCL.SocialService;
using DCL.UI.InputFieldFormatting;
using DCL.UI.MainUI;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using MVC;
using System.Threading;
using ECS;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : IDCLGlobalPlugin<ChatPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IChatHistory chatHistory;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly NametagsData nametagsData;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly MainUIView mainUIView;
        private readonly IRoomHub roomHub;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly IProfileCache profileCache;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ChatMessageFactory chatMessageFactory;
        private ChatHistoryStorage? chatStorage;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly ObjectProxy<FriendsCache> friendsCacheProxy;
        private readonly IRPCSocialServices socialServiceProxy;
        private readonly IFriendsEventBus friendsEventBus;
        private readonly ObjectProxy<IFriendsService> friendsServiceProxy;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private ChatController chatController;
        private readonly ChatUserStateUpdater chatUserStateUpdater;
        private ChatMainPresenter chatMainPresenter;
        private IRealmData realmData;
        private IRealmNavigator realmNavigator;

        public ChatPlugin(
            IMVCManager mvcManager,
            IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            NametagsData nametagsData,
            MainUIView mainUIView,
            IInputBlock inputBlock,
            Arch.Core.World world,
            Entity playerEntity,
            IRoomHub roomHub,
            IAssetsProvisioner assetsProvisioner,
            ITextFormatter hyperlinkTextFormatter,
            IProfileCache profileCache,
            IChatEventBus chatEventBus,
            IWeb3IdentityCache web3IdentityCache,
            ILoadingStatus loadingStatus,
            ISharedSpaceManager sharedSpaceManager,
            ObjectProxy<IUserBlockingCache> userBlockingCacheProxy,
            IRPCSocialServices socialServiceProxy,
            IFriendsEventBus friendsEventBus,
            ChatMessageFactory chatMessageFactory,
            ProfileRepositoryWrapper profileDataProvider,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            IRealmData realmData,
            IRealmNavigator realmNavigator)
        {
            this.mvcManager = mvcManager;
            this.chatHistory = chatHistory;
            this.chatMessagesBus = chatMessagesBus;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.inputBlock = inputBlock;
            this.world = world;
            this.playerEntity = playerEntity;
            this.assetsProvisioner = assetsProvisioner;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
            this.roomHub = roomHub;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatMessageFactory = chatMessageFactory;
            this.friendsServiceProxy = friendsServiceProxy;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.socialServiceProxy = socialServiceProxy;
            this.friendsEventBus = friendsEventBus;
            this.profileRepositoryWrapper = profileDataProvider;
            this.realmData = realmData;
            this.realmNavigator = realmNavigator;
        }

        public void Dispose()
        {
            chatStorage?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ChatPluginSettings settings, CancellationToken ct)
        {
            ProvidedAsset<ChatSettingsAsset> chatSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatSettingsAsset, ct);
            var privacySettings = new RPCChatPrivacyService(socialServiceProxy, chatSettingsAsset.Value);

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE))
            {
                string walletAddress = web3IdentityCache.Identity != null ? web3IdentityCache.Identity.Address : string.Empty;
                chatStorage = new ChatHistoryStorage(chatHistory, chatMessageFactory, walletAddress);
            }

            var chatService = new ChatService(chatHistory,
                friendsServiceProxy,
                chatStorage,
                chatUserStateUpdater);

            var chatMemberService = new ChatMemberListService(roomHub,
                profileCache,
                friendsServiceProxy);
            
            var presenterFactory = new ChatPresenterFactory(
                chatHistory,
                chatMessagesBus,
                chatEventBus,
                profileCache,
                web3IdentityCache,
                entityParticipantTable,
                roomHub,
                world,
                chatSettingsAsset.Value,
                nametagsData,
                friendsServiceProxy,
                userBlockingCacheProxy,
                profileRepositoryWrapper,
                privacySettings,
                chatUserStateUpdater,
                friendsEventBus,
                friendsServiceProxy,
                chatService,
                chatMemberService
            );

            chatMainPresenter = new ChatMainPresenter(
                () =>
                {
                    var view = mainUIView.ChatView2;
                    view.gameObject.SetActive(true);
                    return view;
                },
                presenterFactory,
                chatHistory,
                chatMessagesBus,
                friendsServiceProxy,
                chatService,
                chatMemberService
            );
            
            // chatController = new ChatController(
            //     () =>
            //     {
            //         ChatView? view = mainUIView.ChatView;
            //         view.gameObject.SetActive(false);
            //         return view;
            //     },
            //     chatMessagesBus,
            //     chatHistory,
            //     entityParticipantTable,
            //     nametagsData,
            //     world,
            //     playerEntity,
            //     inputBlock,
            //     roomHub,
            //     chatSettingsAsset.Value,
            //     hyperlinkTextFormatter,
            //     profileCache,
            //     chatEventBus,
            //     web3IdentityCache,
            //     loadingStatus,
            //     userBlockingCacheProxy,
            //     privacySettings,
            //     friendsEventBus,
            //     chatStorage,
            //     friendsServiceProxy,
            //     profileRepositoryWrapper
            // );

            //sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Chat, chatController);
            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Chat, chatMainPresenter);
            mvcManager.RegisterController(chatMainPresenter);

            // Log out / log in
            web3IdentityCache.OnIdentityCleared += OnIdentityCleared;
            loadingStatus.CurrentStage.OnUpdate += OnLoadingStatusUpdate;
        }

        private void OnLoadingStatusUpdate(LoadingStatus.LoadingStage status)
        {
            if (status == LoadingStatus.LoadingStage.Completed)
                sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, false)).Forget();
        }

        private void OnIdentityCleared()
        {
            if (chatController.IsVisibleInSharedSpace)
                chatController.HideViewAsync(CancellationToken.None).Forget();
        }
    }

    public class ChatPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceT<ChatSettingsAsset> ChatSettingsAsset { get; private set; }
    }
}
