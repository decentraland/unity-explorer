using DCL.Audio;
using DCL.Chat.History;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;

namespace DCL.Chat
{
    public class ChatControllerMessageHandlingHelper
    {
        private readonly IChatHistory chatHistory;
        private readonly IChatController chatController;
        private readonly ChatControllerChatBubblesHelper chatBubblesHelper;
        private readonly ChatSettingsAsset chatSettings;
        private readonly ITextFormatter hyperlinkTextFormatter;

        private bool hasToResetUnreadMessagesWhenNewMessageArrive;
        private int messageCountWhenSeparatorViewed;

        public ChatControllerMessageHandlingHelper(
            IChatHistory chatHistory,
            IChatController chatController,
            ChatControllerChatBubblesHelper chatBubblesHelper,
            ChatSettingsAsset chatSettings,
            ITextFormatter hyperlinkTextFormatter)
        {
            this.chatHistory = chatHistory;
            this.chatController = chatController;
            this.chatBubblesHelper = chatBubblesHelper;
            this.chatSettings = chatSettings;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
        }

        public void OnChatHistoryMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage)
        {
            bool isSentByOwnUser = addedMessage is { IsSystemMessage: false, IsSentByOwnUser: true };

            chatBubblesHelper.CreateChatBubble(destinationChannel, addedMessage, isSentByOwnUser);

            if (isSentByOwnUser)
            {
                MarkCurrentChannelAsRead();
                if (chatController.TryGetView(out var view))
                {
                    view.RefreshMessages();
                    view.ShowLastMessage();
                }
                return;
            }

            HandleMessageAudioFeedback(addedMessage);

            if (chatController.TryGetView(out var currentView))
            {
                bool shouldMarkChannelAsRead = currentView is { IsMessageListVisible: true, IsScrollAtBottom: true };
                bool isCurrentChannel = destinationChannel.Id.Equals(currentView.CurrentChannelId);

                if (isCurrentChannel)
                {
                    if (shouldMarkChannelAsRead)
                        MarkCurrentChannelAsRead();

                    HandleUnreadMessagesSeparator(destinationChannel);
                    currentView.RefreshMessages();
                }
                else
                {
                    currentView.RefreshUnreadMessages(destinationChannel.Id);
                }
            }
        }

        public void OnChatBusMessageAdded(ChatChannel.ChannelId channelId, ChatMessage chatMessage)
        {
            if (!chatMessage.IsSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(chatMessage.Message);
                var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, chatMessage);
                chatHistory.AddMessage(channelId, newChatMessage);
            }
            else
                chatHistory.AddMessage(channelId, chatMessage);
        }

        private void HandleMessageAudioFeedback(ChatMessage message)
        {
            if (!chatController.TryGetView(out var view))
                return;

            switch (chatSettings.chatAudioSettings)
            {
                case ChatAudioSettings.NONE:
                    return;
                case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
                case ChatAudioSettings.ALL:
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(message.IsMention ?
                        view.ChatReceiveMentionMessageAudio :
                        view.ChatReceiveMessageAudio);
                    break;
            }
        }

        private void HandleUnreadMessagesSeparator(ChatChannel channel)
        {
            if (!hasToResetUnreadMessagesWhenNewMessageArrive)
                return;

            hasToResetUnreadMessagesWhenNewMessageArrive = false;
            channel.ReadMessages = messageCountWhenSeparatorViewed;
        }

        public void OnChatHistoryReadMessagesChanged(ChatChannel changedChannel)
        {
            if (chatController.TryGetView(out var view))
            {
                if (changedChannel.Id.Equals(view.CurrentChannelId))
                    view.RefreshMessages();
                else
                    view.RefreshUnreadMessages(changedChannel.Id);
            }
        }

        public void OnUnreadMessagesSeparatorViewed()
        {
            if (chatController.TryGetView(out var view))
            {
                messageCountWhenSeparatorViewed = chatHistory.Channels[view.CurrentChannelId].Messages.Count;
                hasToResetUnreadMessagesWhenNewMessageArrive = true;
            }
        }

        public void OnScrollBottomReached()
        {
            MarkCurrentChannelAsRead();
        }

        public void MarkCurrentChannelAsRead()
        {
            if (chatController.TryGetView(out var view))
            {
                chatHistory.Channels[view.CurrentChannelId].MarkAllMessagesAsRead();
                messageCountWhenSeparatorViewed = chatHistory.Channels[view.CurrentChannelId].ReadMessages;
            }
        }

        public void ClearChannel()
        {
            if (chatController.TryGetView(out var view))
            {
                chatHistory.ClearChannel(view.CurrentChannelId);
                messageCountWhenSeparatorViewed = 0;
            }
        }
    }
}
