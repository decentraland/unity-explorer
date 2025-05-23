using System;

namespace DCL.Chat.History
{
    public readonly struct ChatMessage : IEquatable<ChatMessage>
    {
        private const string DCL_SYSTEM_SENDER = "DCL System";

        public readonly string Message;
        public readonly string SenderValidatedName;
        public readonly string SenderWalletId;
        public readonly string SenderWalletAddress;
        public readonly bool IsPaddingElement;
        public readonly bool IsSentByOwnUser;
        public readonly bool IsSystemMessage;
        public readonly bool IsMention;
        public readonly string CommunityId;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string senderWalletAddress,
            bool isSentByOwnUser,
            string senderWalletId,
            string communityId,
            bool isMention = false,
            bool isSystemMessage = false,
            bool isPaddingElement = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            SenderWalletAddress = senderWalletAddress;
            IsSentByOwnUser = isSentByOwnUser;
            IsPaddingElement = isPaddingElement;
            SenderWalletId = senderWalletId;
            IsMention = isMention;
            IsSystemMessage = isSystemMessage;
            CommunityId = communityId;
        }

        public static ChatMessage NewPaddingElement() =>
            new (string.Empty,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                string.Empty,
                false,
                false,
                true);

        public static ChatMessage CopyWithNewMessage(string newMessage, ChatMessage chatMessage) =>
            new (newMessage,
                chatMessage.SenderValidatedName,
                chatMessage.SenderWalletAddress,
                chatMessage.IsSentByOwnUser,
                chatMessage.SenderWalletId,
                chatMessage.CommunityId,
                chatMessage.IsMention,
                chatMessage.IsSystemMessage,
                chatMessage.IsPaddingElement);

        public static ChatMessage NewFromSystem(string message) =>
            new (message, DCL_SYSTEM_SENDER, string.Empty, true,
                null, string.Empty, false, true, false);

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
                   SenderWalletAddress == other.SenderWalletAddress &&
                   IsSentByOwnUser == other.IsSentByOwnUser &&
                   IsMention == other.IsMention &&
                   CommunityId == other.CommunityId;
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
                SenderWalletAddress, IsSentByOwnUser, IsMention);
        }

        public override string ToString() =>
            IsPaddingElement ? "[Padding]" :
            IsSystemMessage ? $"[System] {Message}" :
            $"[{SenderValidatedName}] {Message}";
    }
}
