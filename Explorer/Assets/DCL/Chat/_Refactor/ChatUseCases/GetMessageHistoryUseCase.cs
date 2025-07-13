using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.UI.InputFieldFormatting;

namespace DCL.Chat.ChatUseCases
{
    public class MessageHistoryResult
    {
        public List<ChatMessageViewModel> Messages { get; set; }
    }

    public class GetMessageHistoryUseCase
    {
        private readonly IChatHistory chatHistory;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CreateMessageViewModelUseCase createMessageViewModelUseCase;

        public GetMessageHistoryUseCase(
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            CreateMessageViewModelUseCase createMessageViewModelUseCase)
        {
            this.chatHistory = chatHistory;
            this.chatHistoryStorage = chatHistoryStorage;
            this.createMessageViewModelUseCase = createMessageViewModelUseCase;
        }

        public async UniTask<MessageHistoryResult> ExecuteAsync(ChatChannel.ChannelId channelId, CancellationToken token)
        {
            // 1. Ensure messages are loaded from disk if necessary
            if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channelId))
            {
                await chatHistoryStorage.InitializeChannelWithMessagesAsync(channelId);
            }

            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
            {
                return new MessageHistoryResult
                {
                    Messages = new List<ChatMessageViewModel>()
                };
            }

            // 2. Perform all the data processing logic here
            var processedMessages = new List<ChatMessage>(channel.Messages);
            processedMessages.RemoveAll(msg => msg.IsPaddingElement);

            int unreadCount = processedMessages.Count - channel.ReadMessages;
            bool needsSeparator = unreadCount > 0 && channel.ReadMessages > 0;

            if (needsSeparator)
            {
                int nonPaddingMessagesCount = 0;
                // This logic is complex and error-prone, a perfect candidate for a unit test!
                for (int i = 0; i < channel.ReadMessages; i++)
                {
                    if (!channel.Messages[i].IsPaddingElement) nonPaddingMessagesCount++;
                }

                if (nonPaddingMessagesCount < processedMessages.Count)
                    processedMessages.Insert(nonPaddingMessagesCount, ChatMessage.NewSeparator());
            }

            // 3. Format the final list into ViewModels
            var viewModels = new List<ChatMessageViewModel>();
            foreach (var msg in processedMessages)
            {
                // The use case is responsible for creating the view model
                viewModels.Add(createMessageViewModelUseCase.Execute(msg));
            }

            return new MessageHistoryResult
            {
                Messages = viewModels
            };
        }
    }
}