using DCL.Audio;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Settings.Settings;
using DCL.UI.InputFieldFormatting;
using System;
using System.Globalization;
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
            bool isCopyOfSystemMessage = IsCopyOfSystemMessage(message.Message);
            bool isSystemMessage = message.IsSystemMessage;

            if (!isSystemMessage && !isCopyOfSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(message.Message);
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
                case ChatAudioSettings.NONE:
                    return;
                case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
                case ChatAudioSettings.ALL:
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<char> TrimStartIgnorables(ReadOnlySpan<char> s)
        {
            int i = 0;
            while (i < s.Length)
            {
                char ch = s[i];

                // skip whitespace (includes NBSP, newlines)
                if (char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }

                // skip format/control-like invisibles (ZWSP, ZWJ, LRM, RLM, BOM, VS16, etc.)
                var cat = char.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.Format || ch == '\uFEFF')
                {
                    i++;
                    continue;
                }

                break;
            }

            return s.Slice(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsCopyOfSystemMessage(string message)
        {
            // normalize the front edge
            var s = TrimStartIgnorables(message.AsSpan());

            // compare EXACTLY (ordinal), not culture-aware
            if (s.StartsWith("🟢".AsSpan(), StringComparison.Ordinal)) return AfterMarkerLooksLikeSystem(s, "🟢");
            if (s.StartsWith("🔴".AsSpan(), StringComparison.Ordinal)) return AfterMarkerLooksLikeSystem(s, "🔴");
            if (s.StartsWith("🟡".AsSpan(), StringComparison.Ordinal)) return AfterMarkerLooksLikeSystem(s, "🟡");

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AfterMarkerLooksLikeSystem(ReadOnlySpan<char> s, string marker)
        {
            // Require a space or punctuation right after the marker.
            var rest = s.Slice(marker.AsSpan().Length);
            return rest.Length == 0 || char.IsWhiteSpace(rest[0]) || ":-—–,;.!?)]}".AsSpan().IndexOf(rest[0]) >= 0;
        }
    }
}
