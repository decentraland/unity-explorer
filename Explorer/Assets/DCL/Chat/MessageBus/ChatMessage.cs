using System;

namespace DCL.Chat
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
        public readonly bool HasToAnimate;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string walletAddress,
            bool sentByOwnUser,
            bool hasToAnimate,
            string senderWalletId,
            bool systemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            WalletAddress = walletAddress;
            SentByOwnUser = sentByOwnUser;
            IsPaddingElement = false;
            HasToAnimate = hasToAnimate;
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
            HasToAnimate = true;
            SystemMessage = false;
        }

        public static ChatMessage NewFromSystem(string message) =>
            new (message, "DCL System", string.Empty, true,
                false, null, true);

        public bool Equals(ChatMessage other) =>
            IsPaddingElement == other.IsPaddingElement &&
            Message == other.Message &&
            SenderValidatedName == other.SenderValidatedName &&
            SenderWalletId == other.SenderWalletId &&
            WalletAddress == other.WalletAddress &&
            SentByOwnUser == other.SentByOwnUser &&
            SystemMessage == other.SystemMessage &&
            HasToAnimate == other.HasToAnimate;

        public override bool Equals(object? obj) =>
            obj is ChatMessage other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IsPaddingElement, Message, SenderValidatedName, SenderWalletId,
                WalletAddress, SentByOwnUser, SystemMessage, HasToAnimate);
    }
}
