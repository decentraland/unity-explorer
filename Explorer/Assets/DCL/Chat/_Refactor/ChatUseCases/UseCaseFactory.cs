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
    public class UseCaseFactory : IDisposable
    {
        private readonly ChatConfig chatConfig;
        private readonly EventSubscriptionScope scope = new();
        
        public InitializeChatSystemUseCase InitializeChat { get; }
        public CreateMessageViewModelUseCase CreateMessageViewModel { get; }
        public GetUserChatStatusUseCase GetUserChatStatus { get; }
        public SelectChannelUseCase SelectChannel { get; }
        public GetMessageHistoryUseCase GetMessageHistory { get; }
        public MarkChannelAsReadUseCase MarkChannelAsRead { get; }
        public GetTitlebarViewModelUseCase GetTitlebarViewModel { get; }
        public GetProfileThumbnailUseCase GetProfileThumbnail { get; }
        public SendMessageUseCase SendMessage { get; }
        public LeaveChannelUseCase LeaveChannel { get; }
        public CreateChannelViewModelUseCase CreateChannelViewModel { get; }
        public GetChannelMembersUseCase GetChannelMembersUseCase { get; set; }

        public UseCaseFactory(
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
            InitializeChat = new InitializeChatSystemUseCase(eventBus,
                chatHistory,
                friendsServiceProxy,
                chatHistoryStorage,
                chatUserStateUpdater,
                currentChannelService);

            CreateMessageViewModel = new CreateMessageViewModelUseCase(textFormatter);

            GetUserChatStatus = new GetUserChatStatusUseCase(chatUserStateUpdater,
                eventBus);
            
            SelectChannel = new SelectChannelUseCase(eventBus,
                chatHistory,
                currentChannelService);
            
            GetMessageHistory = new GetMessageHistoryUseCase(chatHistory,
                chatHistoryStorage,
                CreateMessageViewModel);
            
            MarkChannelAsRead = new MarkChannelAsReadUseCase(eventBus,
                chatHistory);

            GetProfileThumbnail = new GetProfileThumbnailUseCase(eventBus,
                chatConfig,
                profileRepositoryWrapper);
            
            GetChannelMembersUseCase = new GetChannelMembersUseCase(chatMemberListService,
                GetProfileThumbnail);
            
            GetTitlebarViewModel = new GetTitlebarViewModelUseCase(eventBus,
                profileRepositoryWrapper,
                GetProfileThumbnail,
                chatConfig);
            
            SendMessage = new SendMessageUseCase(
                chatMessageBus,
                currentChannelService);
            
            LeaveChannel = new LeaveChannelUseCase(eventBus,
                chatHistory,
                currentChannelService,
                SelectChannel);

            CreateChannelViewModel = new CreateChannelViewModelUseCase(eventBus,
                chatConfig,
                profileRepositoryWrapper);
        }
    
        public void Dispose()
        {
            scope.Dispose();
        }
    }
}