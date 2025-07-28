using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;

namespace DCL.Chat.ChatUseCases
{
    public class GetMessageHistoryCommand
    {
        private readonly IChatHistory chatHistory;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CreateMessageViewModelCommand createMessageViewModelCommand;

        public GetMessageHistoryCommand(
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            CreateMessageViewModelCommand createMessageViewModelCommand)
        {
            this.chatHistory = chatHistory;
            this.chatHistoryStorage = chatHistoryStorage;
            this.createMessageViewModelCommand = createMessageViewModelCommand;
        }

        public async UniTask ExecuteAsync(List<ChatMessageViewModel> targetList, ChatChannel.ChannelId channelId, CancellationToken token)
        {
            // Return all elements from the list to the pool
            targetList.ForEach(ChatMessageViewModel.RELEASE);
            targetList.Clear();

            if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channelId))
            {
                await chatHistoryStorage.InitializeChannelWithMessagesAsync(channelId);
                chatHistory.Channels[channelId].MarkAllMessagesAsRead();
            }

            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return;

            foreach (ChatMessage channelMessage in channel.Messages)
                targetList.Add(createMessageViewModelCommand.Execute(channelMessage));
        }
    }
}
