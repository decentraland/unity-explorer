namespace DCL.Chat.ChatReactions.Networking
{
    public readonly struct ReactionReceivedArgs
    {
        public readonly string WalletId;
        public readonly int EmojiIndex;
        public readonly int Count;
        public readonly ReactionType Type;
        public readonly string MessageId;
        public readonly bool IsRemoval;

        public ReactionReceivedArgs(string walletId, int emojiIndex, int count, ReactionType type, string messageId, bool isRemoval = false)
        {
            WalletId = walletId;
            EmojiIndex = emojiIndex;
            Count = count;
            Type = type;
            MessageId = messageId;
            IsRemoval = isRemoval;
        }
    }
}
