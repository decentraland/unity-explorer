using System;
using DCL.Audio;
using DCL.Chat.ChatUseCases.DCL.Chat.ChatUseCases;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Communities;
using DCL.Friends;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;

using Utility;

namespace DCL.Chat.ChatUseCases
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

        public CommandRegistry(
            ChatConfig chatConfig,
            ChatSettingsAsset chatSettings,
            IEventBus eventBus,
            IChatMessagesBus chatMessageBus,
            CommunitiesEventBus communitiesEventBus,
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            ChatUserStateUpdater chatUserStateUpdater,
            ICurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            CommunitiesDataProvider communitiesDataProvider,
            ICommunityDataService communityDataService,
            ITextFormatter textFormatter,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ISpriteCache spriteCache,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            AudioClipConfig sendMessageSound,
            GetParticipantProfilesCommand getParticipantProfilesCommand)
        {
            GetParticipantProfilesCommand = getParticipantProfilesCommand;

            InitializeChat = new InitializeChatSystemCommand(eventBus,
                chatHistory,
                communitiesEventBus,
                friendsServiceProxy,
                chatHistoryStorage,
                communitiesDataProvider,
                communityDataService,
                chatUserStateUpdater,
                currentChannelService);

            CreateMessageViewModel = new CreateMessageViewModelCommand(profileRepositoryWrapper, chatConfig);

            SelectChannel = new SelectChannelCommand(eventBus,
                chatHistory,
                currentChannelService);

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

            GetUserChatStatusCommand = new GetUserChatStatusCommand(chatUserStateUpdater,
                eventBus);

            OpenConversation = new OpenConversationCommand(eventBus,
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

            CloseChannel = new CloseChannelCommand(chatHistory);

            CreateChannelViewModel = new CreateChannelViewModelCommand(eventBus,
                communityDataService,
                chatConfig,
                profileRepositoryWrapper,
                GetUserChatStatusCommand,
                GetCommunityThumbnail);
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
