using System;

namespace DCL.Chat.History
{
    public readonly struct ChatMessage : IEquatable<ChatMessage>
    {
        public readonly bool IsPaddingElement;
        public readonly string Message;
        public readonly string SenderValidatedName;
        public readonly string SenderWalletId;
        public readonly string WalletAddress;
        public readonly bool SentByOwnUser;
        public readonly bool SystemMessage;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string walletAddress,
            bool sentByOwnUser,
            string senderWalletId,
            bool systemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            SenderWalletId = senderWalletId;
            SystemMessage = systemMessage;
        }

        public ChatMessage(bool isPaddingElement)
        {
            IsPaddingElement = isPaddingElement;
            SenderWalletId = string.Empty;
            Message = string.Empty;
            SenderValidatedName = string.Empty;
            WalletAddress = string.Empty;
            SentByOwnUser = false;
            SystemMessage = false;
        }

        public static ChatMessage NewFromSystem(string message) =>
            new (message, "DCL System", string.Empty, true,
                null, true);

        public bool Equals(ChatMessage other) =>
            IsPaddingElement == other.IsPaddingElement &&
            Message == other.Message &&
            SenderValidatedName == other.SenderValidatedName &&
            SenderWalletId == other.SenderWalletId &&
            WalletAddress == other.WalletAddress &&
            SentByOwnUser == other.SentByOwnUser &&
            SystemMessage == other.SystemMessage;

        public override bool Equals(object? obj) =>
            obj is ChatMessage other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IsPaddingElement, Message, SenderValidatedName, SenderWalletId,
                WalletAddress, SentByOwnUser, SystemMessage);
    }
}
