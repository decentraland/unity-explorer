using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Chat.ChatCommands
{
    public class GetMessageHistoryCommand
    {
        private const string NEW_CHAT_MESSAGE = "The chat starts here! Time to say hi! \\U0001F44B";

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

                if (chatHistory.Channels[channelId].Messages.Count == 0)
                    chatHistory.AddMessage(channelId, chatHistory.Channels[channelId].ChannelType,
                        ChatMessage.NewFromSystem(NEW_CHAT_MESSAGE));

                chatHistory.Channels[channelId].MarkAllMessagesAsRead();
            }

            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return;

            for (var index = 0; index < channel.Messages.Count; index++)
            {
                ChatMessage channelMessage = channel.Messages[index];
                (bool isTopMostMessage, ChatMessage? previousMessage) = GetTopMostAndPreviousMessage(channel.Messages, index);
                targetList.Add(createMessageViewModelCommand.Execute(channelMessage, previousMessage, isTopMostMessage));
            }
        }

        internal static (bool isTopMost, ChatMessage? previousMessage) GetTopMostAndPreviousMessage(
            IReadOnlyList<ChatMessage> messages, int index)
        {
            bool isTopMost = index == messages.Count - 1;
            ChatMessage? previousMessage = isTopMost ? null : messages[index + 1];
            return (isTopMost, previousMessage);
        }
    }
}
