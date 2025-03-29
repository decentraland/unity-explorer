using UnityEngine;

namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly bool IsMention;
        public readonly string SenderDisplayName;
        public readonly string RecipientValidatedName;
        public readonly string RecipientWalletId;
        public readonly string SenderWalletId;
        public readonly string ChannelId;
        public readonly bool IsPrivateMessage;
        public readonly bool IsOwnMessage;
        public bool IsDirty;
        public readonly Color RecipientNameColor;
        public ChatBubbleComponent(
            string chatMessage,
            string senderDisplayName,
            string senderWalletId,
            bool isMention,
            bool isPrivateMessage,
            string channelId,
            bool isOwnMessage,
            string recipientValidatedName,
            string recipientWalletId,
            Color recipientNameColor)
        {
            ChatMessage = chatMessage;
            SenderDisplayName = senderDisplayName;
            SenderWalletId = senderWalletId;
            IsMention = isMention;
            IsPrivateMessage = isPrivateMessage;
            ChannelId = channelId;
            IsOwnMessage = isOwnMessage;
            RecipientValidatedName = recipientValidatedName;
            RecipientWalletId = recipientWalletId;
            RecipientNameColor = recipientNameColor;
            IsDirty = true;
        }
    }
}
