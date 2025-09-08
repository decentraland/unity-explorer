using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using System;
using System.Runtime.CompilerServices;
using DCL.Translation.Service;

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
        private readonly ITranslationService translationService;
        private readonly ChatSettingsAsset chatSettings;

        public ChatHistoryService(IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            ITextFormatter hyperlinkTextFormatter,
            ChatConfig.ChatConfig chatConfig,
            ChatSettingsAsset chatSettings,
            ITranslationService translationService)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.chatConfig = chatConfig;
            this.chatSettings = chatSettings;
            this.chatHistory = chatHistory;
            this.translationService = translationService;

            chatMessagesBus.MessageAdded += OnChatMessageAdded;
        }

        public void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatMessageAdded;
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            if (type == ChatChannel.ChatChannelType.COMMUNITY && !chatHistory.Channels.ContainsKey(channel))
                return;

            var messageToAdd = message;
            if (!message.IsSystemMessage && !IsCopyOfSystemMessage(message.Message))
            {
                string formattedText = hyperlinkTextFormatter.FormatText(message.Message);
                messageToAdd = ChatMessage.CopyWithNewMessage(formattedText, message);
            }

            chatHistory.AddMessage(channel, type, messageToAdd);

            if (!messageToAdd.IsSystemMessage && !messageToAdd.IsSentByOwnUser)
            {
                translationService.ProcessIncomingMessage(messageToAdd.MessageId,
                    messageToAdd.Message,
                    channel.Id);
            }
            
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
