using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;

namespace DCL.Chat.ChatUseCases
{
    public class MessageHistoryResult
    {
        public List<ChatMessageViewModel> ViewModelMessages { get; set; }
    }

    public class GetMessageHistoryCommand
    {
        private readonly IChatHistory chatHistory;
        private readonly ChatHistoryStorage? chatHistoryStorage;
        private readonly CreateMessageViewModelCommand _createMessageViewModelCommand;

        public GetMessageHistoryCommand(
            IChatHistory chatHistory,
            ChatHistoryStorage? chatHistoryStorage,
            CreateMessageViewModelCommand createMessageViewModelCommand)
        {
            this.chatHistory = chatHistory;
            this.chatHistoryStorage = chatHistoryStorage;
            this._createMessageViewModelCommand = createMessageViewModelCommand;
        }

        public async UniTask<IReadOnlyList<ChatMessage>> GetChatMessagesExecuteAsync(ChatChannel.ChannelId channelId, CancellationToken token)
        {
            if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channelId))
                await chatHistoryStorage.InitializeChannelWithMessagesAsync(channelId);

            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
                return new List<ChatMessage>();

            var processedMessages = new List<ChatMessage>();
            var originalMessages = channel.Messages;

            int nonPaddingReadCount = 0;
            int nonPaddingTotalCount = 0;
            foreach (var msg in originalMessages)
                if (!msg.IsPaddingElement)
                    nonPaddingTotalCount++;

            int unreadCount = nonPaddingTotalCount - channel.ReadMessages;
            bool needsSeparator = unreadCount > 0 && channel.ReadMessages > 0;
            bool separatorInserted = false;

            for (int i = 0; i < originalMessages.Count; i++)
            {
                var msg = originalMessages[i];

                if (!msg.IsPaddingElement)
                {
                    if (needsSeparator && !separatorInserted && originalMessages.Count - i <= unreadCount)
                    {
                        processedMessages.Add(ChatMessage.NewSeparator());
                        separatorInserted = true;
                    }
                }

                processedMessages.Add(msg);
            }

            return processedMessages;
        }
        
        public async UniTask<MessageHistoryResult> ExecuteAsync(ChatChannel.ChannelId channelId, CancellationToken token)
        {
            if (chatHistoryStorage != null && !chatHistoryStorage.IsChannelInitialized(channelId))
                await chatHistoryStorage.InitializeChannelWithMessagesAsync(channelId);
            
            token.ThrowIfCancellationRequested();

            if (!chatHistory.Channels.TryGetValue(channelId, out var channel))
            {
                return new MessageHistoryResult
                {
                    ViewModelMessages = new List<ChatMessageViewModel>()
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
                viewModels.Add(_createMessageViewModelCommand.Execute(msg));
            }

            return new MessageHistoryResult
            {
                ViewModelMessages = viewModels
            };
        }
    }
}