using System;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Friends;
using DCL.Profiles;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using Utilities;

namespace DCL.Chat.ChatUseCases
{
    public class CommandRegistry : IDisposable
    {
        private readonly ChatConfig chatConfig;
        private readonly EventSubscriptionScope scope = new();
        
        public InitializeChatSystemCommand InitializeChat { get; }
        public CreateMessageViewModelCommand CreateMessageViewModel { get; }
        public GetUserChatStatusCommand GetUserChatStatus { get; }
        public SelectChannelCommand SelectChannel { get; }
        public GetMessageHistoryCommand GetMessageHistory { get; }
        public MarkChannelAsReadCommand MarkChannelAsRead { get; }
        public GetTitlebarViewModelCommand GetTitlebarViewModel { get; }
        public GetProfileThumbnailCommand GetProfileThumbnail { get; }
        public SendMessageCommand SendMessage { get; }
        public LeaveChannelCommand LeaveChannel { get; }
        public CreateChannelViewModelCommand CreateChannelViewModel { get; }
        public GetChannelMembersCommand GetChannelMembersCommand { get; set; }

        public CommandRegistry(
            ChatConfig chatConfig,
            IEventBus eventBus,
            IChatMessagesBus chatMessageBus,
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            ChatUserStateUpdater chatUserStateUpdater,
            ICurrentChannelService currentChannelService,
            ChatMemberListService chatMemberListService,
            ITextFormatter textFormatter,
            IProfileCache profileCache,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ObjectProxy<IFriendsService> friendsServiceProxy
        )
        {
            InitializeChat = new InitializeChatSystemCommand(eventBus,
                chatHistory,
                friendsServiceProxy,
                chatHistoryStorage,
                chatUserStateUpdater,
                currentChannelService);

            CreateMessageViewModel = new CreateMessageViewModelCommand(textFormatter);

            GetUserChatStatus = new GetUserChatStatusCommand(chatUserStateUpdater,
                eventBus);
            
            SelectChannel = new SelectChannelCommand(eventBus,
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
            
            GetChannelMembersCommand = new GetChannelMembersCommand(chatMemberListService,
                GetProfileThumbnail);
            
            GetTitlebarViewModel = new GetTitlebarViewModelCommand(eventBus,
                profileRepositoryWrapper,
                GetProfileThumbnail,
                chatConfig);
            
            SendMessage = new SendMessageCommand(
                chatMessageBus,
                currentChannelService);
            
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