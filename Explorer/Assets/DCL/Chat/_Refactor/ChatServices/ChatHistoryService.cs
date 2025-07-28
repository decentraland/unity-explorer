using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.UI.InputFieldFormatting;
using System;

namespace DCL.Chat.Services
{
    /// <summary>
    ///     Listens to the bus and adds a formatted message to the chat history
    /// </summary>
    public class ChatHistoryService : IDisposable
    {
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IChatHistory chatHistory;
        private readonly ITextFormatter hyperlinkTextFormatter;
        private readonly ChatConfig chatConfig;

        public ChatHistoryService(IChatMessagesBus chatMessagesBus, IChatHistory chatHistory, ITextFormatter hyperlinkTextFormatter, ChatConfig chatConfig)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.hyperlinkTextFormatter = hyperlinkTextFormatter;
            this.chatConfig = chatConfig;
            this.chatHistory = chatHistory;

            chatMessagesBus.MessageAdded += OnChatMessageAdded;
        }

        public void Dispose()
        {
            chatMessagesBus.MessageAdded -= OnChatMessageAdded;
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            // TODO communities logic
            // if (channelType == ChatChannel.ChatChannelType.COMMUNITY && !userCommunities.ContainsKey(channelId))
            // return;

            if (!message.IsSystemMessage)
            {
                string formattedText = hyperlinkTextFormatter.FormatText(message.Message);
                var newChatMessage = ChatMessage.CopyWithNewMessage(formattedText, message);
                chatHistory.AddMessage(channel, type, newChatMessage);
            }
            else
                chatHistory.AddMessage(channel, type, message);
        }

        private void HandleMessageAudioFeedback(ChatMessage message)
        {
            // TODO
            // Move audio to the chat config
            // play it here: it has nothing to do with the view

            // if (IsViewReady)
            //     return;
            //
            // switch (chatSettings.chatAudioSettings)
            // {
            //     case ChatAudioSettings.NONE:
            //         return;
            //     case ChatAudioSettings.MENTIONS_ONLY when message.IsMention:
            //     case ChatAudioSettings.ALL:
            //         UIAudioEventsBus.Instance.SendPlayAudioEvent(message.IsMention ?
            //             viewInstance!.ChatReceiveMentionMessageAudio :
            //             viewInstance!.ChatReceiveMessageAudio);
            //         break;
            // }
        }
    }
}
