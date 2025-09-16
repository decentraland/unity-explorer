using System;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.EventBus;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider;
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
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatConfig;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatServices.ChatTranslationService.Tests;
using DCL.Clipboard;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Translation.Service;
using DCL.Translation.Service.Cache;
using DCL.Translation.Service.Memory;
using DCL.Translation.Service.Policy;
using DCL.Translation.Service.Provider;
using DCL.Translation.Settings;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

using Utility;

namespace DCL.PluginSystem.Global
{
    public class ChatPlugin : IDCLGlobalPlugin<ChatPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IChatHistory chatHistory;
        private readonly ClipboardManager clipboardManager;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IEventBus eventBus;
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
        private readonly CommunityDataService communityDataService;
        private readonly ISpriteCache thumbnailCache;
        private readonly CommunitiesEventBus communitiesEventBus;
        private ChatController chatController;
        private readonly IMVCManagerMenusAccessFacade mvcManagerMenusAccessFacade;
        private ChatMainController chatMainController;
        private PrivateConversationUserStateService? chatUserStateService;
        private ChatHistoryService? chatBusListenerService;
        private CommunityUserStateService communityUserStateService;
        private readonly Transform chatViewRectTransform;
        private readonly EventSubscriptionScope pluginScope = new ();
        private readonly CancellationTokenSource pluginCts;
        private CommandRegistry commandRegistry;
        private readonly bool includeTranslationChat;
        private FallbackFontsProvider fallbackFontsProvider;
        private ITranslationSettings translationSettings;
        private ITranslationMemory translationMemory;
        private ITranslationService translationService;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly DecentralandEnvironment decentralandEnvironment;

        public ChatPlugin(
            IMVCManager mvcManager,
            IMVCManagerMenusAccessFacade mvcManagerMenusAccessFacade,
            IChatMessagesBus chatMessagesBus,
            IEventBus eventBus,
            IChatHistory chatHistory,
            ClipboardManager clipboardManager,
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
            CommunityDataService communityDataService,
            ISpriteCache thumbnailCache,
            WarningNotificationView warningNotificationView,
            CommunitiesEventBus communitiesEventBus,
            IVoiceChatCallStatusService voiceChatCallStatusService,
            bool isCallEnabled,
            bool includeTranslationChat,
            IRealmNavigator realmNavigator,
            Transform chatViewRectTransform,
            ITranslationSettings translationSettings,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            DecentralandEnvironment decentralandEnvironment)
        {
            this.mvcManager = mvcManager;
            this.mvcManagerMenusAccessFacade = mvcManagerMenusAccessFacade;
            this.chatMessagesBus = chatMessagesBus;
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.clipboardManager = clipboardManager;
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
            this.communitiesEventBus = communitiesEventBus;
            this.voiceChatCallStatusService = voiceChatCallStatusService;
            this.isCallEnabled = isCallEnabled;
            this.includeTranslationChat = includeTranslationChat;
            this.chatViewRectTransform = chatViewRectTransform;
            this.translationSettings = translationSettings;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.decentralandEnvironment = decentralandEnvironment;
            
            pluginCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            chatStorage?.Dispose();
            chatBusListenerService?.Dispose();
            chatUserStateService?.Dispose();
            communityUserStateService?.Dispose();
            fallbackFontsProvider?.Dispose();

            pluginScope.Dispose();
            pluginCts.Cancel();
            pluginCts.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ChatPluginSettings settings, CancellationToken ct)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, pluginCts.Token);

            fallbackFontsProvider = new FallbackFontsProvider(assetsProvisioner, settings.FallbackFonts, linkedCts.Token);

            var privacySettings = new RPCChatPrivacyService(socialServiceProxy, settings.ChatSettingsAsset);

            var chatConfigAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.ChatConfig, linkedCts.Token);
            var chatConfig = chatConfigAsset.Value;

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_HISTORY_LOCAL_STORAGE))
            {
                string walletAddress = web3IdentityCache.Identity != null ? web3IdentityCache.Identity.Address : string.Empty;
                chatStorage = new ChatHistoryStorage(chatHistory, chatMessageFactory, walletAddress);
            }

            var translationPolicy = new ConversationTranslationPolicy(translationSettings);

            var translationProvider = new DclTranslationProvider(webRequestController, decentralandUrlsSource, translationSettings);
            //var translationProvider = new MockTranslationProvider();
            
            var translationCache = new InMemoryTranslationCache();
            translationMemory = new InMemoryTranslationMemory();

            translationService = new TranslationService(translationProvider,
                translationCache,
                translationPolicy,
                translationSettings,
                eventBus,
                translationMemory);

            var translationTester = new GameObject("_TranslationTester")
                .AddComponent<TranslationTester>();

            translationTester.Initialize(translationService,
                translationProvider,
                translationSettings);

            var viewInstance = mainUIView.ChatView2;
            var chatWorldBubbleService = new ChatWorldBubbleService(world,
                playerEntity,
                entityParticipantTable,
                profileCache,
                nametagsData,
                settings.ChatSettingsAsset,
                chatHistory,
                communityDataService);

            var currentChannelService = new CurrentChannelService();

            chatUserStateService = new PrivateConversationUserStateService(
                currentChannelService,
                eventBus,
                userBlockingCacheProxy,
                friendsServiceProxy,
                settings.ChatSettingsAsset,
                privacySettings,
                friendsEventBus,
                roomHub.ChatRoom());

            var chatInputBlockingService = new ChatInputBlockingService(inputBlock, world);

            // Ignore buttons that would lead to the conflicting state
            var chatClickDetectionService = new ChatClickDetectionService((RectTransform)viewInstance.transform,
                viewInstance.TitlebarView.CloseChatButton.transform,
                viewInstance.TitlebarView.CloseMemberListButton.transform,
                viewInstance.TitlebarView.OpenMemberListButton.transform,
                viewInstance.TitlebarView.BackFromMemberList.transform,
                viewInstance.InputView.inputField.transform,
                chatViewRectTransform);

            var chatContextMenuService = new ChatContextMenuService(mvcManagerMenusAccessFacade,
                chatClickDetectionService);

            var nearbyUserStateService = new NearbyUserStateService(roomHub, eventBus);
            communityUserStateService = new CommunityUserStateService(communityDataProvider,
                communitiesEventBus,
                eventBus,
                chatHistory,
                web3IdentityCache);

            var chatMemberService = new ChatMemberListService(profileRepositoryWrapper,
                friendsServiceProxy,
                currentChannelService,
                eventBus);

            var getParticipantProfilesCommand = new GetParticipantProfilesCommand(roomHub, profileCache);

            commandRegistry = new CommandRegistry(
                chatConfig,
                settings.ChatSettingsAsset,
                eventBus,
                web3IdentityCache,
                chatEventBus,
                chatMessagesBus,
                chatHistory,
                chatStorage,
                chatMemberService,
                nearbyUserStateService,
                communityUserStateService,
                chatUserStateService,
                currentChannelService,
                communityDataProvider,
                communityDataService,
                profileRepositoryWrapper,
                thumbnailCache,
                friendsServiceProxy,
                settings.ChatSendMessageAudio,
                getParticipantProfilesCommand,
                clipboardManager,
                translationService,
                translationMemory,
                translationSettings);

            pluginScope.Add(commandRegistry);

            chatMainController = new ChatMainController(
                () =>
                {
                    ChatMainView? view = mainUIView.ChatView2;
                    view.gameObject.SetActive(false);
                    return view;
                },
                chatConfig,
                eventBus,
                mvcManager,
                chatMessagesBus,
                chatEventBus,
                currentChannelService,
                chatInputBlockingService,
                commandRegistry,
                chatHistory,
                profileRepositoryWrapper,
                chatMemberService,
                chatContextMenuService,
                communityDataService,
                translationSettings,
                translationMemory,
                chatClickDetectionService
            );

            chatBusListenerService = new ChatHistoryService(chatMessagesBus,
                chatHistory,
                hyperlinkTextFormatter,
                chatConfig,
                settings.ChatSettingsAsset,
                translationService);

            pluginScope.Add(chatMainController);
            pluginScope.Add(chatWorldBubbleService);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.Chat, chatMainController);
            mvcManager.RegisterController(chatMainController);

            // Log out / log in
            web3IdentityCache.OnIdentityCleared += OnIdentityCleared;
            web3IdentityCache.OnIdentityChanged += OnIdentityChanged;

            loadingStatus.CurrentStage.OnUpdate += OnLoadingStatusUpdate;
        }

        private void OnLoadingStatusUpdate(LoadingStatus.LoadingStage status)
        {
            if (status == LoadingStatus.LoadingStage.Completed)
                sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, false)).Forget();
        }

        private void OnIdentityCleared()
        {
            ReportHub.Log(ReportData.UNSPECIFIED, "ChatPlugin.OnIdentityCleared");
            commandRegistry.ResetChat.Execute();

            if (chatMainController.IsVisibleInSharedSpace)
                chatMainController.HideViewAsync(CancellationToken.None).Forget();
        }

        private void OnIdentityChanged()
        {
            if (web3IdentityCache.Identity == null) return;

            ReportHub.Log(ReportData.UNSPECIFIED, "ChatPlugin.OnIdentityChanged: Re-initializing chat system for new user.");

            ReinitializeChatAsync(pluginCts.Token).Forget();
        }

        private async UniTaskVoid ReinitializeChatAsync(CancellationToken ct)
        {
            try
            {
                // STEP 1: RE-CONFIGURE SESSION-SPECIFIC SERVICES
                chatStorage?.SetNewLocalUserWalletAddress(web3IdentityCache.EnsuredIdentity().Address);

                // STEP 2: RESTART BACKGROUND SERVICES
                await commandRegistry.RestartChatServices.ExecuteAsync(ct);
                ct.ThrowIfCancellationRequested();

                // STEP 3: RE-POPULATE DATA
                // Run the original initialization command. This command is now critical.
                // It will call chatStorage.LoadAllChannelsWithoutMessages(), which will now
                // read from the correct user directory because we re-configured it in Step 1.
                // It will also fetch friends and communities for the new user.
                await commandRegistry.InitializeChat.ExecuteAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES);
            }
        }
    }

    public class ChatPluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public ChatSettingsAsset ChatSettingsAsset { get; private set; }
        [field: SerializeField] public AssetReferenceT<ChatConfig> ChatConfig { get; private set; }
        [field: SerializeField] public List<AssetReferenceT<TMP_FontAsset>> FallbackFonts { get; private set; }

        [Header("Audio")]
        [field: SerializeField] public AudioClipConfig ChatSendMessageAudio { get; private set; }
    }
}
