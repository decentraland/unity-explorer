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
using DCL.UI;
using DCL.UI.InputFieldFormatting;
using DCL.UI.MainUI;
using DCL.Web3.Identities;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.VoiceChat;
using MVC;
using System.Threading;
using DCL.Audio;
using DCL.Chat.ChatUseCases;
using DCL.Chat.Services;
using DCL.Chat.Services.DCL.Chat;
using DCL.Communities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using UnityEngine.AddressableAssets;

using Utility;

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
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;
        private readonly bool isCallEnabled;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly ICommunityDataService communityDataService;
        private readonly ISpriteCache thumbnailCache;
        private readonly WarningNotificationView warningNotificationView;
        private readonly CommunitiesEventBus communitiesEventBus;
        private ChatController chatController;
        private readonly IMVCManagerMenusAccessFacade mvcManagerMenusAccessFacade;
        private ChatMainController chatMainController;
        private ChatUserStateUpdater chatUserStateUpdater;
        private readonly IEventBus eventBus = new EventBus();
        private readonly EventSubscriptionScope pluginScope = new ();

        public ChatPlugin(
            IMVCManager mvcManager,
            IMVCManagerMenusAccessFacade mvcManagerMenusAccessFacade,
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
            CommunitiesDataProvider communitiesDataProvider,
            ICommunityDataService communityDataService,
            ISpriteCache thumbnailCache,
            WarningNotificationView warningNotificationView,
            CommunitiesEventBus communitiesEventBus,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            bool includeVoiceChat)
        {
            this.mvcManager = mvcManager;
            this.mvcManagerMenusAccessFacade = mvcManagerMenusAccessFacade;
            this.chatMessagesBus = chatMessagesBus;
            this.chatHistory = chatHistory;
            this.entityParticipantTable = entityParticipantTable;
            this.nametagsData = nametagsData;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
            this.world = world;
            this.playerEntity = playerEntity;
            this.roomHub = roomHub;
            this.assetsProvisioner = assetsProvisioner;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.profileCache = profileCache;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.loadingStatus = loadingStatus;
            this.sharedSpaceManager = sharedSpaceManager;
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.socialServiceProxy = socialServiceProxy; //
            this.friendsEventBus = friendsEventBus;
            this.chatMessageFactory = chatMessageFactory;
            this.profileRepositoryWrapper = profileDataProvider;
            this.friendsServiceProxy = friendsServiceProxy;
            communityDataProvider = communitiesDataProvider;
            this.communityDataService = communityDataService;
            this.thumbnailCache = thumbnailCache;
            this.warningNotificationView = warningNotificationView;
            this.communitiesEventBus = communitiesEventBus;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            isCallEnabled = includeVoiceChat;
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

            var chatConfigAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatConfig, ct);
            var chatConfig = chatConfigAsset.Value;

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE))
            {
                string walletAddress = web3IdentityCache.Identity != null ? web3IdentityCache.Identity.Address : string.Empty;
                chatStorage = new ChatHistoryStorage(chatHistory, chatMessageFactory, walletAddress);
            }

            var viewInstance = mainUIView.ChatView2;
            
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
            
            

            var chatInputBlockingService = new ChatInputBlockingService(inputBlock, world);
            
            // Ignore buttons that would lead to the conflicting state
            var chatClickDetectionService = new ChatClickDetectionService((RectTransform)viewInstance.transform,
                viewInstance.TitlebarView.CloseChatButton.transform,
                viewInstance.TitlebarView.CloseMemberListButton.transform,
                viewInstance.TitlebarView.OpenMemberListButton.transform,
                viewInstance.TitlebarView.BackFromMemberList.transform,
                viewInstance.InputView.inputField.transform);

            var chatContextMenuService = new ChatContextMenuService(mvcManagerMenusAccessFacade,
                chatClickDetectionService);

            var getUserChatStatus = new GetUserChatStatusCommand(chatUserStateUpdater, eventBus);

            var currentChannelService = new CurrentChannelService(getUserChatStatus);

            var chatMemberService = new ChatMemberListService(roomHub,
                profileCache,
                friendsServiceProxy,
                currentChannelService,
                communityDataProvider,
                web3IdentityCache);
            
            var chatUserStateBridge =
                new ChatUserStateBridge(chatUserStateEventBus, eventBus, currentChannelService);

            var getParticipantProfilesCommand = new GetParticipantProfilesCommand(roomHub, profileCache);
            
            var useCaseFactory = new CommandRegistry(
                chatConfig,
                chatSettingsAsset.Value,
                eventBus,
                chatMessagesBus,
                communitiesEventBus,
                chatHistory,
                chatStorage,
                chatUserStateUpdater,
                currentChannelService,
                chatMemberService,
                communityDataProvider,
                communityDataService,
                hyperlinkTextFormatter,
                profileRepositoryWrapper,
                thumbnailCache,
                friendsServiceProxy,
                settings.ChatSendMessageAudio,
                getParticipantProfilesCommand
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
                chatMessagesBus,
                chatEventBus,
                chatUserStateEventBus,
                chatUserStateBridge,
                currentChannelService,
                chatInputBlockingService,
                chatSettingsAsset.Value,
                useCaseFactory,
                chatHistory,
                profileRepositoryWrapper,
                chatMemberService,
                chatContextMenuService,
                chatClickDetectionService
            );

            pluginScope.Add(chatMainController);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Chat, chatMainController);
            mvcManager.RegisterController(chatMainController);

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

        [Header("Audio")]
        [field: SerializeField] public AudioClipConfig ChatSendMessageAudio { get; private set; }
    }
}
