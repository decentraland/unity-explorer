﻿using DCL.Audio;
using DCL.Chat.ChatCommands.DCL.Chat.ChatUseCases;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Communities;
using DCL.Friends;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using System;
using DCL.Chat.EventBus;
using DCL.Communities.CommunitiesDataProvider;
using DCL.VoiceChat;
using DCL.Clipboard;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Translation;
using DCL.Translation.Service;
using DCL.Web3.Identities;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class CommandRegistry : IDisposable
    {
        private readonly EventSubscriptionScope scope = new();

        public InitializeChatSystemCommand InitializeChat { get; }
        public CreateMessageViewModelCommand CreateMessageViewModel { get; }
        public SelectChannelCommand SelectChannel { get; }
        public GetMessageHistoryCommand GetMessageHistory { get; }
        public MarkMessagesAsReadCommand MarkMessagesAsRead { get; }
        public GetTitlebarViewModelCommand GetTitlebarViewModel { get; }
        public GetCommunityThumbnailCommand GetCommunityThumbnail { get; }
        public SendMessageCommand SendMessage { get; }
        public CloseChannelCommand CloseChannel { get; }
        public CreateChannelViewModelCommand CreateChannelViewModel { get; }
        public OpenConversationCommand OpenConversation { get; }
        public DeleteChatHistoryCommand DeleteChatHistory { get; }
        public GetChannelMembersCommand GetChannelMembersCommand { get; }
        public GetParticipantProfilesCommand GetParticipantProfilesCommand { get; }
        public GetUserChatStatusCommand GetUserChatStatusCommand { get; }
        public ResetChatCommand ResetChat { get; }
        public RestartChatServicesCommand RestartChatServices { get; }
        public ResolveInputStateCommand ResolveInputStateCommand { get; }
        public GetUserCallStatusCommand GetUserCallStatusCommand { get; }
        public ToggleAutoTranslateCommand ToggleAutoTranslateCommand { get; }
        public TranslateMessageCommand TranslateMessageCommand { get; }
        public RevertToOriginalCommand RevertToOriginalCommand { get; }

        public CommandRegistry(
            ChatConfig.ChatConfig chatConfig,
            ChatSettingsAsset chatSettings,
            IEventBus eventBus,
            IWeb3IdentityCache identityCache,
            IChatEventBus chatEventBus,
            IChatMessagesBus chatMessageBus,
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            ChatMemberListService chatMemberListService,
            NearbyUserStateService nearbyUserStateService,
            CommunityUserStateService communityUserStateService,
            PrivateConversationUserStateService privateConversationUserStateService,
            CurrentChannelService currentChannelService,
            CommunitiesDataProvider communitiesDataProvider,
            ICommunityDataService communityDataService,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ISpriteCache spriteCache,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            AudioClipConfig sendMessageSound,
            GetParticipantProfilesCommand getParticipantProfilesCommand,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            ClipboardManager clipboardManager,
            ITranslationService translationService,
            ITranslationMemory translationMemory,
            ITranslationCache translationCache,
            ITranslationSettings translationSettings)
        {
            RestartChatServices = new RestartChatServicesCommand(
                privateConversationUserStateService,
                communityUserStateService,
                chatMemberListService);

            ResetChat = new ResetChatCommand(eventBus,
                chatHistory,
                chatHistoryStorage,
                currentChannelService,
                privateConversationUserStateService,
                communityUserStateService,
                chatMemberListService,
                translationMemory,
                translationCache);

            GetParticipantProfilesCommand = getParticipantProfilesCommand;

            InitializeChat = new InitializeChatSystemCommand(eventBus,
                identityCache,
                chatHistory,
                friendsServiceProxy,
                chatHistoryStorage,
                communitiesDataProvider,
                communityDataService,
                privateConversationUserStateService,
                currentChannelService,
                nearbyUserStateService,
                chatMemberListService);

            CreateMessageViewModel = new CreateMessageViewModelCommand(profileRepositoryWrapper,
                chatConfig,
                translationMemory);

            SelectChannel = new SelectChannelCommand(eventBus,
                chatEventBus,
                chatHistory,
                currentChannelService,
                communityUserStateService,
                nearbyUserStateService,
                privateConversationUserStateService);

            DeleteChatHistory = new DeleteChatHistoryCommand(eventBus,
                chatHistory,
                currentChannelService);

            GetMessageHistory = new GetMessageHistoryCommand(chatHistory,
                chatHistoryStorage,
                CreateMessageViewModel);

            MarkMessagesAsRead = new MarkMessagesAsReadCommand();

            GetCommunityThumbnail = new GetCommunityThumbnailCommand(spriteCache,
                chatConfig);

            GetChannelMembersCommand = new GetChannelMembersCommand(chatConfig);

            GetUserChatStatusCommand = new GetUserChatStatusCommand(privateConversationUserStateService,
                eventBus);

            OpenConversation = new OpenConversationCommand(eventBus,
                identityCache,
                chatHistory,
                SelectChannel);

            GetTitlebarViewModel = new GetTitlebarViewModelCommand(eventBus,
                communityDataService,
                profileRepositoryWrapper,
                chatConfig,
                GetUserChatStatusCommand,
                GetCommunityThumbnail);

            SendMessage = new SendMessageCommand(
                chatMessageBus,
                currentChannelService,
                sendMessageSound,
                chatSettings);

            CloseChannel = new CloseChannelCommand(chatHistory,
                identityCache);

            CreateChannelViewModel = new CreateChannelViewModelCommand(eventBus,
                communityDataService,
                chatConfig,
                profileRepositoryWrapper,
                GetUserChatStatusCommand,
                GetCommunityThumbnail,
                voiceChatOrchestrator);

            ResolveInputStateCommand = new ResolveInputStateCommand(GetUserChatStatusCommand,
                currentChannelService);

            ToggleAutoTranslateCommand = new ToggleAutoTranslateCommand(translationSettings,
                eventBus);

            TranslateMessageCommand = new TranslateMessageCommand(translationService);
            RevertToOriginalCommand = new RevertToOriginalCommand(translationService);

            GetUserCallStatusCommand = new GetUserCallStatusCommand(privateConversationUserStateService);
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
