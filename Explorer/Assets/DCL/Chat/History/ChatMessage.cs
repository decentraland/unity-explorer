using System;

namespace DCL.Chat.History
{
    public readonly struct ChatMessage : IEquatable<ChatMessage>
    {
        private const string DCL_SYSTEM_SENDER = "DCL System";

        public readonly string Message;
        public readonly string SenderValidatedName;
        public readonly string SenderWalletId;
        public readonly string WalletAddress;
        public readonly ChatChannel.ChannelId ChannelId;
        public readonly bool IsPaddingElement;
        public readonly bool IsSentByOwnUser;
        public readonly bool IsSystemMessage;
        public readonly bool IsMention;
        public readonly bool IsPrivateMessage;


        public ChatMessage(
            string message,
            string senderValidatedName,
            string walletAddress,
            bool isSentByOwnUser,
            string senderWalletId,
            ChatChannel.ChannelId channelId,
            bool isPrivateMessage = false,
            bool isMention = false,
            bool isSystemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            WalletAddress = walletAddress;
            IsSentByOwnUser = isSentByOwnUser;
            IsPaddingElement = false;
            SenderWalletId = senderWalletId;
            ChannelId = channelId;
            IsPrivateMessage = isPrivateMessage;
            IsMention = isMention;
            IsSystemMessage = isSystemMessage;
        }

        public static ChatMessage NewPaddingElement() =>
            new (string.Empty, string.Empty, string.Empty, false, string.Empty, ChatChannel.EMPTY_CHANNEL_ID);

        public static ChatMessage CopyWithNewMessage(string message, ChatMessage chatMessage) =>
            new (message, chatMessage.SenderValidatedName, chatMessage.WalletAddress, chatMessage.IsSentByOwnUser, chatMessage.SenderWalletId, chatMessage.ChannelId, chatMessage.IsMention, chatMessage.IsSystemMessage);

        public static ChatMessage NewFromSystem(string message) =>
            new (message, DCL_SYSTEM_SENDER, string.Empty, true,
                null, ChatChannel.NEARBY_CHANNEL_ID,false, true);

        public bool Equals(ChatMessage other)
        {
            if (IsPaddingElement != other.IsPaddingElement)
                return false;
            if (IsPaddingElement)
                return true;

            if (IsSystemMessage != other.IsSystemMessage)
                return false;
            if (IsSystemMessage)
                return Message == other.Message;

            return Message == other.Message &&
                   SenderValidatedName == other.SenderValidatedName &&
                   SenderWalletId == other.SenderWalletId &&
                   WalletAddress == other.WalletAddress &&
                   IsSentByOwnUser == other.IsSentByOwnUser &&
                   IsMention == other.IsMention;
        }

        public override bool Equals(object? obj) =>
            obj is ChatMessage other && Equals(other);

        public override int GetHashCode()
        {
            if (IsPaddingElement)
                return 1;

            if (IsSystemMessage)
                return HashCode.Combine(Message, true);

            return HashCode.Combine(Message, SenderValidatedName, SenderWalletId,
                WalletAddress, IsSentByOwnUser, IsMention);
        }

        public override string ToString() =>
            IsPaddingElement ? "[Padding]" :
            IsSystemMessage ? $"[System] {Message}" :
            $"[{SenderValidatedName}] {Message}";
    }
}
