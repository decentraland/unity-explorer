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
        public readonly bool IsSentByOwnUser;
        public readonly bool IsSystemMessage;
        public readonly bool IsMention;

        /// <summary>
        /// The instant when the message was sent (UTC), in OLE Automation Date format. Zero means null or unassigned.
        /// </summary>
        public readonly double SentTimestampRaw;

        public readonly DateTime? SentTimestamp;

        public ChatMessage(
            string message,
            string senderValidatedName,
            string senderWalletAddress,
            bool isSentByOwnUser,
            string senderWalletId,
            double sentTimestamp,
            bool isMention = false,
            bool isSystemMessage = false)
        {
            Message = message;
            SenderValidatedName = senderValidatedName;
            SenderWalletAddress = senderWalletAddress;
            IsSentByOwnUser = isSentByOwnUser;
            SenderWalletId = senderWalletId;
            IsMention = isMention;
            IsSystemMessage = isSystemMessage;
            SentTimestampRaw = sentTimestamp;
            SentTimestamp = sentTimestamp != 0.0 ? DateTime.FromOADate(sentTimestamp) : null;
        }

        public static ChatMessage CopyWithNewMessage(string newMessage, ChatMessage chatMessage) =>
            new (newMessage,
                chatMessage.SenderValidatedName,
                chatMessage.SenderWalletAddress,
                chatMessage.IsSentByOwnUser,
                chatMessage.SenderWalletId,
                DateTime.UtcNow.ToOADate(),
                chatMessage.IsMention,
                false);

        public static ChatMessage NewFromSystem(string message) =>
            new (message, DCL_SYSTEM_SENDER, string.Empty, true,
                null, DateTime.UtcNow.ToOADate(), false, true);

        public bool Equals(ChatMessage other)
        {
            if (IsSystemMessage != other.IsSystemMessage)
                return false;
            if (IsSystemMessage)
                return Message == other.Message;

            return Message == other.Message &&
                   SenderValidatedName == other.SenderValidatedName &&
                   SenderWalletId == other.SenderWalletId &&
                   SenderWalletAddress == other.SenderWalletAddress &&
                   IsSentByOwnUser == other.IsSentByOwnUser &&
                   IsMention == other.IsMention;
        }

        public override bool Equals(object? obj) =>
            obj is ChatMessage other && Equals(other);

        public override int GetHashCode()
        {
            if (IsSystemMessage)
                return HashCode.Combine(Message, true);

            return HashCode.Combine(Message, SenderValidatedName, SenderWalletId,
                SenderWalletAddress, IsSentByOwnUser, IsMention);
        }

        public override string ToString() =>
            IsSystemMessage ? $"[System] {Message}" :
            $"[{SenderValidatedName}] {Message}";
    }
}
