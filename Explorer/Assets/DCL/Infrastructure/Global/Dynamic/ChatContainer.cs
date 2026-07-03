using Arch.Core;
using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Chat.ChatServices;
using DCL.Chat.Commands;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.ChatArea;
using DCL.Communities;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UserBlocking;
using DCL.Nametags;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.Translation;
using DCL.UI.InputFieldFormatting;
using DCL.Utilities;
using DCL.VoiceChat;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic.ChatCommands;
using Global.Versioning;
using MVC;
using SceneRunner.Debugging.Hub;
using System;
using System.Collections.Generic;

namespace Global.Dynamic
{
    /// <summary>
    ///     Chat backbone: history, message bus with commands, chat event buses and the chat teleporter.
    /// </summary>
    public class ChatContainer : IDisposable
    {
        public ChatHistory ChatHistory { get; }

        public IChatMessagesBus ChatMessagesBus { get; }

        public ChatEventBus ChatEventBus { get; }

        public ChatTeleporter ChatTeleporter { get; }

        public ChatMessageFactory ChatMessageFactory { get; }

        public CurrentChannelService CurrentChannelService { get; }

        public ChatSharedAreaEventBus ChatSharedAreaEventBus { get; }

        public ReloadSceneChatCommand ReloadSceneChatCommand { get; }

        public PlayerPrefsTranslationSettings TranslationSettings { get; }

        private ChatContainer(
            ChatHistory chatHistory,
            IChatMessagesBus chatMessagesBus,
            ChatEventBus chatEventBus,
            ChatTeleporter chatTeleporter,
            ChatMessageFactory chatMessageFactory,
            CurrentChannelService currentChannelService,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ReloadSceneChatCommand reloadSceneChatCommand,
            PlayerPrefsTranslationSettings translationSettings)
        {
            ChatHistory = chatHistory;
            ChatMessagesBus = chatMessagesBus;
            ChatEventBus = chatEventBus;
            ChatTeleporter = chatTeleporter;
            ChatMessageFactory = chatMessageFactory;
            CurrentChannelService = currentChannelService;
            ChatSharedAreaEventBus = chatSharedAreaEventBus;
            ReloadSceneChatCommand = reloadSceneChatCommand;
            TranslationSettings = translationSettings;
        }

        public static ChatContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            UIShellContainer uiShellContainer,
            CommsContainer commsContainer,
            ProfileContainer profileContainer,
            IWeb3IdentityCache identityCache,
            IUserBlockingCache userBlockingCache,
            IWorldInfoHub worldInfoHub,
            ECSReloadScene reloadSceneController,
            TeleportController teleportController,
            IRealmNavigator realmNavigator,
            IDebugContainerBuilder debugBuilder,
            DCLVersion dclVersion,
            IAppArgs appArgs,
            World globalWorld,
            Entity playerEntity,
            bool localSceneDevelopment,
            bool enableAnalytics)
        {
            var chatHistory = new ChatHistory();
            var chatEventBus = new ChatEventBus();

            var chatTeleporter = new ChatTeleporter(realmNavigator, new ChatEnvironmentValidator(bootstrapContainer.Environment), bootstrapContainer.DecentralandUrlsSource);

            var reloadSceneChatCommand = new ReloadSceneChatCommand(reloadSceneController, globalWorld, playerEntity, staticContainer.ScenesCache, teleportController, localSceneDevelopment);

            var chatMessageFactory = new ChatMessageFactory(staticContainer.ProfilesContainer.Cache, identityCache);

            var currentChannelService = new CurrentChannelService();

            var chatCommands = new List<IChatCommand>
            {
                new GoToChatCommand(chatTeleporter, staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.DecentralandUrlsSource),
                new GoToLocalChatCommand(chatTeleporter),
                new DebugPanelChatCommand(debugBuilder),
                new ShowEntityChatCommand(worldInfoHub),
                reloadSceneChatCommand,
                new LoadPortableExperienceChatCommand(staticContainer.PortableExperiencesController),
                new KillPortableExperienceChatCommand(staticContainer.PortableExperiencesController, staticContainer.SmartWearableCache),
                new VersionChatCommand(dclVersion),
                new SupportChatCommand(uiShellContainer.SupportRequestService),
                new RoomsChatCommand(commsContainer.RoomHub),
                new LogsChatCommand(),
                new CacheChatCommand(),
                new SceneAdminsChatCommand(),
                new AppArgsCommand(appArgs),
                new LogMatrixChatCommand((RuntimeReportsHandlingSettings)bootstrapContainer.DiagnosticsContainer.Settings),
                new AnrSimulateChatCommand(),
#if UNITY_STANDALONE_WIN
                new AnrDumpChatCommand(),
#endif
            };

            chatCommands.Add(new HelpChatCommand(chatCommands, appArgs));

            IChatMessagesBus coreChatMessageBus = new LiveKitChatMessagesBus(commsContainer.MessagePipesHub, chatMessageFactory, userBlockingCache, bootstrapContainer.Environment, identityCache, commsContainer.RoomHub)
                                                 .WithSelfResend(identityCache, chatMessageFactory)
                                                 .WithIgnoreSymbols()
                                                 .WithCommands(chatCommands, staticContainer.LoadingStatus)
                                                 .WithDebugPanel(debugBuilder);

            IChatMessagesBus chatMessagesBus = enableAnalytics
                ? new ChatMessagesBusAnalyticsDecorator(coreChatMessageBus, bootstrapContainer.Analytics.Controller, staticContainer.ProfilesContainer.Cache, profileContainer.SelfProfile)
                : coreChatMessageBus;

            ChatOpener.Initialize(new ChatOpener(chatEventBus, uiShellContainer.MvcManager));

            return new ChatContainer(
                chatHistory,
                chatMessagesBus,
                chatEventBus,
                chatTeleporter,
                chatMessageFactory,
                currentChannelService,
                new ChatSharedAreaEventBus(),
                reloadSceneChatCommand,
                new PlayerPrefsTranslationSettings());
        }

        public ChatPlugin CreatePlugin(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IAssetsProvisioner assetsProvisioner,
            UIShellContainer uiShellContainer,
            CommsContainer commsContainer,
            ProfileContainer profileContainer,
            CommunitiesContainer communitiesContainer,
            VoiceChatContainer voiceChatContainer,
            SocialServicesContainer socialServiceContainer,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            NametagsData nametagsData,
            ITextFormatter hyperlinkTextFormatter,
            IWeb3IdentityCache identityCache,
            IUserBlockingCache userBlockingCache,
            IFriendsEventBus friendsEventBus,
            IFriendsService? friendsService,
            CommunityDataService communitiesDataService,
            World globalWorld,
            Entity playerEntity) =>
            new (
                uiShellContainer.MvcManager,
                menusAccessFacade,
                ChatMessagesBus,
                ChatEventBus,
                ChatHistory,
                commsContainer.EntityParticipantTable,
                nametagsData,
                uiShellContainer.MainUIView,
                staticContainer.InputBlock,
                globalWorld,
                playerEntity,
                commsContainer.RoomHub,
                assetsProvisioner,
                hyperlinkTextFormatter,
                staticContainer.ProfilesContainer.Cache,
                ChatEventBus,
                identityCache,
                staticContainer.LoadingStatus,
                userBlockingCache,
                socialServiceContainer.socialServicesRPC,
                friendsEventBus,
                ChatMessageFactory,
                profileContainer.ProfileRepositoryWrapper,
                friendsService,
                communitiesContainer.DataProvider,
                communitiesDataService,
                profileContainer.ThumbnailCache,
                communitiesContainer.EventBus,
                voiceChatContainer.VoiceChatOrchestrator,
                uiShellContainer.MainUIView.SidebarView.unreadMessagesButton.transform,
                TranslationSettings,
                staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource,
                ChatSharedAreaEventBus,
                commsContainer.MessagePipesHub,
                bootstrapContainer.Environment,
                bootstrapContainer.Analytics.Controller,
                CurrentChannelService);

        public void Dispose()
        {
            ChatMessagesBus.Dispose();
        }
    }
}
