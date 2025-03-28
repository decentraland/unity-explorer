using UnityEngine;

namespace DCL.Chat
{
    public struct ChatBubbleComponent
    {
        public readonly string ChatMessage;
        public readonly bool IsMention;
        public readonly string SenderDisplayName;
        public readonly string ReceiverValidatedName;
        public readonly string ReceiverWalletId;
        public readonly string SenderWalletId;
        public readonly string ChannelId;
        public readonly bool IsPrivateMessage;
        public readonly bool IsOwnMessage;
        public bool IsDirty;
        public readonly Color ReceiverNameColor;
        public ChatBubbleComponent(
            string chatMessage,
            string senderDisplayName,
            string senderWalletId,
            bool isMention,
            bool isPrivateMessage,
            string channelId,
            bool isOwnMessage,
            string receiverValidatedName,
            string receiverWalletId,
            Color receiverNameColor)
        {
            ChatMessage = chatMessage;
            SenderDisplayName = senderDisplayName;
            SenderWalletId = senderWalletId;
            IsMention = isMention;
            IsPrivateMessage = isPrivateMessage;
            ChannelId = channelId;
            IsOwnMessage = isOwnMessage;
            ReceiverValidatedName = receiverValidatedName;
            ReceiverWalletId = receiverWalletId;
            ReceiverNameColor = receiverNameColor;
            IsDirty = true;
        }
    }
}
