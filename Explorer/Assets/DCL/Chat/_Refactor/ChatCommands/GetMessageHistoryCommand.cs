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

            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return;

            // Handle channel initialization based on its type
            switch (channel.ChannelType)
            {
                case ChatChannel.ChatChannelType.USER:
                    // User channels are persisted and need to be loaded from storage
                    if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channelId))
                    {
                        // NOTE: doesn't use cancellation token for loading messages it should though
                        _ = await chatHistoryStorage.InitializeChannelWithMessagesAsync(channelId);
                        token.ThrowIfCancellationRequested();

                        // For a brand new conversation that has just been loaded, add a system message
                        if (channel.Messages.Count == 0)
                        {
                            chatHistory.AddMessage(channelId, channel.ChannelType, ChatMessage.NewFromSystem(NEW_CHAT_MESSAGE));
                        }

                        // When loading history for the first time, mark all messages as read
                        channel.MarkAllMessagesAsRead();
                    }

                    break;

                case ChatChannel.ChatChannelType.NEARBY:
                case ChatChannel.ChatChannelType.COMMUNITY:
                    // Nearby and Community channels are session-based and already initialized in memory.
                    // The initial system message for Nearby is added in InitializeChatSystemCommand.
                    // No special action is needed here.
                    break;
            }

            token.ThrowIfCancellationRequested();

            // Populate the view model list from the in-memory chat history for the channel
            // This part is common for all channel types after they have been initialized
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
