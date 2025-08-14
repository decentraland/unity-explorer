using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using System;

namespace DCL.Chat.ChatServices
{
    /// <summary>
    ///     Listens to the bus and adds a formatted message to the chat history
    /// </summary>
    public class ChatHistoryService : IDisposable
    {
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IChatHistory chatHistory;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatConfig.ChatConfig chatConfig;
        private readonly ChatSettingsAsset chatSettings;

        public ChatHistoryService(IChatMessagesBus chatMessagesBus, IChatHistory chatHistory, ITextFormatter hyperlinkTextFormatter, ChatConfig.ChatConfig chatConfig, ChatSettingsAsset chatSettings)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.chatConfig = chatConfig;
            this.chatSettings = chatSettings;
            this.chatHistory = chatHistory;

            chatMessagesBus.MessageAdded += OnChatMessageAdded;
        }

        public void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatMessageAdded;
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            // Don't create a channel for foreign communities
            // For our communities the channel should be created on join and on initialization
            if (type == ChatChannel.ChatChannelType.COMMUNITY && !chatHistory.Channels.ContainsKey(channel))
                return;

            if (!message.IsSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(message.Message);
                var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, message);
                chatHistory.AddMessage(channel, type, newChatMessage);
            }
            else

                // The system message is formatted apriori
                chatHistory.AddMessage(channel, type, message);

            HandleMessageAudioFeedback(message, channel);
        }

        private void HandleMessageAudioFeedback(ChatMessage message, ChatChannel.ChannelId channelId)
        {
            if (message.IsSentByOwnUser)
                return;

            var settings = ChatUserSettings.GetNotificationPingValuePerChannel(channelId);

            switch (settings)
            {
                case ChatAudioSettings.NONE:
                    return;
                case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
                case ChatAudioSettings.ALL:
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(message.IsMention ? chatConfig.ChatReceiveMentionMessageAudio : chatConfig.ChatReceiveMessageAudio);
                    break;
            }
        }
    }
}
