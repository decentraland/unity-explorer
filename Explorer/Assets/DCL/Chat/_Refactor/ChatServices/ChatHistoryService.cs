using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using DCL.UI;
using DCL.UI.InputFieldFormatting;
using System;
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
        private readonly CurrentChannelService currentChannelService;

        public ChatHistoryService(IChatMessagesBus chatMessagesBus,
            IChatHistory chatHistory,
            ITextFormatter hyperlinkTextFormatter,
            ChatConfig.ChatConfig chatConfig,
            ITranslationService translationService,
            CurrentChannelService currentChannelService)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.chatConfig = chatConfig;
            this.chatHistory = chatHistory;
            this.translationService = translationService;
            this.currentChannelService = currentChannelService;

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

            var messageToAdd = message;

            // Provenance is the only thing that may skip this. A message that merely looks like a system line —
            // one starting with 🟢/🔴/🟡 — is still some peer's, and letting its text opt out of the pipeline let
            // that peer choose whether their own markup got neutralized (SEC-023).
            if (!message.IsSystemMessage)
            {
                // Escaped before formatting, not after: the bubble's content label renders rich text, so the
                // peer's own markup has to be inert before the formatter adds the <link> and <color> runs the
                // chat UI depends on. Escaping afterwards would neutralize those too.
                string neutralizedText = RichTextSanitizer.Escape(message.Message);
                string formattedText = hyperlinkTextFormatter.FormatText(neutralizedText);
                messageToAdd = ChatMessage.CopyWithNewMessage(formattedText, message);
            }

            chatHistory.AddMessage(channel, type, messageToAdd);

            if (!messageToAdd.IsSystemMessage && !messageToAdd.IsSentByOwnUser)
                translationService.ProcessIncomingMessage(messageToAdd.MessageId,
                    messageToAdd.SenderWalletAddress,
                    messageToAdd.Message,
                    channel.Id);

            HandleMessageAudioFeedback(message, channel, type);
        }

        private void HandleMessageAudioFeedback(ChatMessage message, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType type)
        {
            if (message.IsSentByOwnUser)
                return;

            var settings = ChatUserSettings.GetNotificationPingValuePerChannel(channelId);

            switch (settings)
            {
                case ChatAudioSettings.None:
                    return;
                case ChatAudioSettings.MentionsOnly when message.IsMention:
                case ChatAudioSettings.All:
                    PlayMessageAudio(message, channelId);
                    break;
            }
        }

        private void PlayMessageAudio(ChatMessage message, ChatChannel.ChannelId channelId)
        {
            bool isChannelFocused = currentChannelService.CurrentChannelId.Equals(channelId);

            ChatConfig.ChatConfig.ChannelAudioConfig audioConfig = isChannelFocused
                ? chatConfig.FocusedChannelMessageAudioConfig
                : chatConfig.UnfocusedChannelMessageAudioConfig;

            AudioClipConfig clip = message.IsMention
                ? audioConfig.receiveMentionAudio
                : audioConfig.receiveMessageAudio;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(clip);
        }
    }
}
