using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using Utility;
using DCL.Chat.History;
using DCL.UI.InputFieldFormatting;

namespace DCL.Chat.ChatUseCases
{
    /// <summary>
    ///     This command is responsible for the entire lifecycle of a new incoming message:
    ///     1. Processing it (e.g., formatting hyperlinks).
    ///     2. Adding it to the persistent chat history.
    ///     3. Creating its initial ViewModel for immediate display.
    ///     4. Kicking off the async task to load its profile picture.
    /// </summary>
    public class ProcessAndAddMessageCommand
    {
        private readonly IEventBus eventBus;
        private readonly IChatHistory chatHistory;
        private readonly ITextFormatter hyperlinkFormatter;
        private readonly CreateMessageViewModelCommand createMessageViewModelCommand;
        private readonly GetProfileThumbnailCommand getProfileThumbnailCommand;

        public ProcessAndAddMessageCommand(
            IEventBus eventBus,
            IChatHistory chatHistory,
            ITextFormatter hyperlinkFormatter,
            CreateMessageViewModelCommand createMessageViewModelCommand,
            GetProfileThumbnailCommand getProfileThumbnailCommand)
        {
            this.eventBus = eventBus;
            this.chatHistory = chatHistory;
            this.hyperlinkFormatter = hyperlinkFormatter;
            this.createMessageViewModelCommand = createMessageViewModelCommand;
            this.getProfileThumbnailCommand = getProfileThumbnailCommand;
        }

        public ChatMessageViewModel Execute(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType? channelType, ChatMessage rawMessage, CancellationToken ct)
        {
            // 1. Process the message (e.g., format text)
            // This replicates the logic from the old OnChatBusMessageAdded
            string formattedText = hyperlinkFormatter.FormatText(rawMessage.Message);
            var processedMessage = ChatMessage.CopyWithNewMessage(formattedText, rawMessage);

            // 2. Add the processed message to the "source of truth" history service
            chatHistory.AddMessage(channelId, channelType, processedMessage);

            // 3. Create the initial ViewModel
            var viewModel = createMessageViewModelCommand.Execute(processedMessage);

            // 4. Start the async thumbnail loading process
            if (!string.IsNullOrEmpty(viewModel.FaceSnapshotUrl))
                FetchThumbnailAndUpdateAsync(viewModel, ct).Forget();

            // 5. Return the ViewModel for immediate display in the UI
            return viewModel;
        }

        private async UniTaskVoid FetchThumbnailAndUpdateAsync(ChatMessageViewModel viewModel, CancellationToken ct)
        {
            // This is the exact same async update pattern we established before
            var thumbnail = await getProfileThumbnailCommand.ExecuteAsync(viewModel.SenderWalletAddress, viewModel.FaceSnapshotUrl, ct);
            if (ct.IsCancellationRequested) return;

            viewModel.ProfilePicture = thumbnail;
            viewModel.IsLoadingPicture = false;

            eventBus.Publish(new ChatEvents.ChatMessageUpdatedEvent
            {
                ViewModel = viewModel
            });
        }

        public void AddRawMessage(ChatChannel.ChannelId channelId,
            ChatChannel.ChatChannelType? channelType,
            ChatMessage rawMessage)
        {
            ChatMessage messageToAdd;
            if (!rawMessage.IsSystemMessage)
            {
                string formattedText = hyperlinkFormatter.FormatText(rawMessage.Message);
                messageToAdd = ChatMessage.CopyWithNewMessage(formattedText, rawMessage);
            }
            else
            {
                messageToAdd = rawMessage;
            }

            chatHistory.AddMessage(channelId, channelType, messageToAdd);
        }
    }
}