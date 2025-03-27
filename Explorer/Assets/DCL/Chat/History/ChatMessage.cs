using System;

namespace DCL.Chat.History
{
    public readonly struct ChatMessage : IEquatable<ChatMessage>
    {
        private const string DCL_SYSTEM_SENDER = "DCL System";

        public readonly bool IsPaddingElement;
        public readonly string Message;
        public readonly string SenderValidatedName;
        public readonly string SenderWalletId;
        public readonly string WalletAddress;
        public readonly bool SentByOwnUser;
        public readonly bool SystemMessage;
        public readonly bool IsMention;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string walletAddress,
            bool sentByOwnUser,
            string senderWalletId,
            bool isMention = false,
            bool systemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            SenderWalletId = senderWalletId;
            IsMention = isMention;
            SystemMessage = systemMessage;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            IsMention = false;
            SenderWalletId = string.Empty;
            Message = string.Empty;
            SenderValidatedName = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
            SystemMessage = false;
        }

        public static ChatMessage CopyWithNewMessage(string message, ChatMessage chatMessage) =>
            new (message, chatMessage.SenderValidatedName, chatMessage.WalletAddress, chatMessage.SentByOwnUser, chatMessage.SenderWalletId, chatMessage.IsMention, chatMessage.SystemMessage);

        public static ChatMessage NewFromSystem(string message) =>
            new (message, DCL_SYSTEM_SENDER, string.Empty, true,
                null, false, true);

        public bool Equals(ChatMessage other)
        {
            if (IsPaddingElement != other.IsPaddingElement)
                return false;
            if (IsPaddingElement)
                return true;

            if (SystemMessage != other.SystemMessage)
                return false;
            if (SystemMessage)
                return Message == other.Message;

            return Message == other.Message &&
                   SenderValidatedName == other.SenderValidatedName &&
                   SenderWalletId == other.SenderWalletId &&
                   WalletAddress == other.WalletAddress &&
                   SentByOwnUser == other.SentByOwnUser &&
                   IsMention == other.IsMention;
        }

        public override bool Equals(object? obj) =>
            obj is ChatMessage other && Equals(other);

        public override int GetHashCode()
        {
            if (IsPaddingElement)
                return 1;

            if (SystemMessage)
                return HashCode.Combine(Message, true);

            return HashCode.Combine(Message, SenderValidatedName, SenderWalletId,
                WalletAddress, SentByOwnUser, IsMention);
        }

        public override string ToString() =>
            IsPaddingElement ? "[Padding]" :
            SystemMessage ? $"[System] {Message}" :
            $"[{SenderValidatedName}] {Message}";
    }
}
