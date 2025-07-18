using System;
using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Friends;
using DCL.Settings.Settings;
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
        public MarkChannelAsReadCommand MarkChannelAsRead { get; }
        public GetTitlebarViewModelCommand GetTitlebarViewModel { get; }
        public GetProfileThumbnailCommand GetProfileThumbnail { get; }
        public SendMessageCommand SendMessage { get; }
        public LeaveChannelCommand LeaveChannel { get; }
        public CreateChannelViewModelCommand CreateChannelViewModel { get; }
        public OpenPrivateConversationCommand OpenPrivateConversation { get; }
        public DeleteChatHistoryCommand DeleteChatHistory { get; }
        public GetChannelMembersCommand GetChannelMembersCommand { get; }
        public GetParticipantProfilesCommand GetParticipantProfilesCommand { get; }

        public CommandRegistry(
            ChatConfig chatConfig,
            ChatSettingsAsset chatSettings,
            IEventBus eventBus,
            IChatMessagesBus chatMessageBus,
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            ChatUserStateUpdater chatUserStateUpdater,
            ICurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            ITextFormatter textFormatter,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ObjectProxy<IFriendsService> friendsServiceProxy,
            AudioClipConfig sendMessageSound,
            GetParticipantProfilesCommand getParticipantProfilesCommand)
        {
            GetParticipantProfilesCommand = getParticipantProfilesCommand;

            InitializeChat = new InitializeChatSystemCommand(eventBus,
                chatHistory,
                friendsServiceProxy,
                chatHistoryStorage,
                chatUserStateUpdater,
                currentChannelService);

            CreateMessageViewModel = new CreateMessageViewModelCommand(textFormatter);

            SelectChannel = new SelectChannelCommand(eventBus,
                chatHistory,
                currentChannelService);

            DeleteChatHistory = new DeleteChatHistoryCommand(eventBus,
                chatHistory,
                currentChannelService);


            GetMessageHistory = new GetMessageHistoryCommand(chatHistory,
                chatHistoryStorage,
                CreateMessageViewModel);

            MarkChannelAsRead = new MarkChannelAsReadCommand(eventBus,
                chatHistory);

            GetProfileThumbnail = new GetProfileThumbnailCommand(eventBus,
                chatConfig,
                profileRepositoryWrapper);

            GetChannelMembersCommand = new GetChannelMembersCommand(eventBus,
                chatMemberListService,
                GetProfileThumbnail);

            OpenPrivateConversation = new OpenPrivateConversationCommand(eventBus,
                chatHistory,
                SelectChannel);


            GetTitlebarViewModel = new GetTitlebarViewModelCommand(eventBus,
                profileRepositoryWrapper,
                GetProfileThumbnail,
                chatConfig);

            SendMessage = new SendMessageCommand(
                chatMessageBus,
                currentChannelService,
                sendMessageSound,
                chatSettings);

            LeaveChannel = new LeaveChannelCommand(eventBus,
                chatHistory,
                currentChannelService,
                SelectChannel);

            CreateChannelViewModel = new CreateChannelViewModelCommand(eventBus,
                chatConfig,
                profileRepositoryWrapper);
        }

        public void Dispose()
        {
            scope.Dispose();
        }
    }
}
