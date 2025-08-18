using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using System;
using System.Runtime.CompilerServices;

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

            if (!message.IsSystemMessage && !IsCopyOfSystemMessage(message.Message))
            {
                // string formattedText = hyperlinkTextFormatter.FormatText(message.Message);
                // var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, message);
                // chatHistory.AddMessage(channel, type, newChatMessage);

                // --- NEW SANITIZATION LOGIC ---
                // Before formatting, check for and remove status emojis from user messages.
                // This handles cases where a user copies and pastes a system message.
                string messageToFormat = message.Message;

                if (messageToFormat.StartsWith("🟢"))
                    messageToFormat = messageToFormat.Substring("🟢".Length).TrimStart(' ');
                else if (messageToFormat.StartsWith("🔴"))
                    messageToFormat = messageToFormat.Substring("🔴".Length).TrimStart(' ');
                else if (messageToFormat.StartsWith("🟡"))
                    messageToFormat = messageToFormat.Substring("🟡".Length).TrimStart(' ');
                // --- END OF SANITIZATION LOGIC ---

                // Now, format the sanitized text.
                string formattedText = hyperlinkTextFormatter.FormatText(messageToFormat);
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

        /// <summary>
        ///     Determines if a message string is a copy of a system message by checking for status emojis.
        /// </summary>
        /// <param name="message">The message content to check.</param>
        /// <returns>True if the message starts with a known system message emoji.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCopyOfSystemMessage(string message)
        {
            // System messages are identified by starting with one of these status emojis.
            // We check for these to avoid re-formatting a message that a user has copied and pasted.
            return message.StartsWith("🟢") ||
                   message.StartsWith("🔴") ||
                   message.StartsWith("🟡");
        }
    }
}
