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
using DCL.Chat.ChatUseCases;
using DCL.Chat.Services;
using ECS;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utilities;

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
        
        private ChatMainController chatMainController;
        private IRealmData realmData;
        private IRealmNavigator realmNavigator;
        private ChatUserStateUpdater chatUserStateUpdater;
        private readonly IEventBus eventBus = new EventBus();
        private readonly EventSubscriptionScope pluginScope = new ();

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
            
            // TODO: make it load through assets provisioner
            // TODO: not working currently, from some reason
            // ProvidedAsset<ChatConfig> chatConfigAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatConfig, ct);
            var chatConfig = Resources.Load("ChatConfig") as ChatConfig;
            
            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE))
            {
                string walletAddress = web3IdentityCache.Identity != null ? web3IdentityCache.Identity.Address : string.Empty;
                chatStorage = new ChatHistoryStorage(chatHistory, chatMessageFactory, walletAddress);
            }
            
            var chatUserStateEventBus = new ChatUserStateEventBus();
            var chatUserStateUpdater = new ChatUserStateUpdater(
                userBlockingCacheProxy,
                roomHub.ChatRoom().Participants,
                chatSettingsAsset.Value,
                privacySettings,
                chatUserStateEventBus,
                friendsEventBus,
                roomHub.ChatRoom(),
                friendsServiceProxy);

            var chatMemberService = new ChatMemberListService(roomHub,
                profileCache,
                friendsServiceProxy);
            
            var chatInputBlockingService = new ChatInputBlockingService(inputBlock, world);
            
            var currentChannelService = new CurrentChannelService();
            
            var useCaseFactory = new UseCaseFactory(
                chatConfig,
                eventBus,
                chatMessagesBus,
                chatHistory,
                chatStorage,
                chatUserStateUpdater,
                currentChannelService,
                hyperlinkTextFormatter,
                profileCache,
                profileRepositoryWrapper,
                friendsServiceProxy
            );
            pluginScope.Add(useCaseFactory);
            
            chatMainController = new ChatMainController(
                () =>
                {
                    ChatMainView? view = mainUIView.ChatView2;
                    view.gameObject.SetActive(false);
                    return view;
                },
                chatConfig,
                eventBus,
                chatUserStateEventBus,
                currentChannelService,
                chatInputBlockingService,
                chatSettingsAsset.Value,
                useCaseFactory,
                chatHistory,
                profileRepositoryWrapper,
                chatMemberService
            );
            
            pluginScope.Add(chatMainController);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Chat, chatMainController);
            mvcManager.RegisterController(chatMainController);
            
            //await useCaseFactory.InitializeChat.ExecuteAsync(ct);
            
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
            if (chatMainController.IsVisibleInSharedSpace)
                chatMainController.HideViewAsync(CancellationToken.None).Forget();
        }
    }

    public class ChatPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceT<ChatSettingsAsset> ChatSettingsAsset { get; private set; }
        [field: SerializeField] public AssetReferenceT<ChatConfig> ChatConfig { get; private set; }
    }
}
