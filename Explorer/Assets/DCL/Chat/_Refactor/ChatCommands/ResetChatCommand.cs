using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Translation.Service;
using Utility;

namespace DCL.Chat.ChatCommands
{
    public class ResetChatCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CurrentChannelService currentChannelService;
        private readonly PrivateConversationUserStateService privateConversationUserStateService;
        private readonly CommunityUserStateService communityUserStateService;
        private readonly ChatMemberListService chatMemberListService;
        private readonly ITranslationMemory translationMemory;
        private readonly ITranslationCache translationCache;

        public ResetChatCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            CurrentChannelService currentChannelService,
            PrivateConversationUserStateService privateConversationUserStateService,
            CommunityUserStateService communityUserStateService,
            ChatMemberListService chatMemberListService,
            ITranslationMemory translationMemory,
            ITranslationCache translationCache)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.chatHistoryStorage = chatHistoryStorage;
            this.currentChannelService = currentChannelService;
            this.privateConversationUserStateService = privateConversationUserStateService;
            this.communityUserStateService = communityUserStateService;
            this.chatMemberListService = chatMemberListService;
            this.translationMemory = translationMemory;
            this.translationCache = translationCache;
        }

        public void Execute()
        {
            chatMemberListService.Stop();
            privateConversationUserStateService.Reset();
            communityUserStateService.Reset();
            chatHistory.DeleteAllChannels();
            currentChannelService.Reset();
            translationMemory.Clear();
            translationCache.Clear();
            chatHistoryStorage?.UnloadAllFiles();

            eventBus.Publish(new ChatEvents.ChatResetEvent());
        }
    }
}