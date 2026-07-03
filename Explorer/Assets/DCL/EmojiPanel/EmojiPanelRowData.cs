namespace DCL.Emoji
{
    public readonly struct EmojiPanelRowData
    {
        public readonly EmojiPanelRowType Type;
        public readonly string HeaderTitle;
        public readonly int EmojiStartIndex;
        public readonly int EmojiCount;

        private EmojiPanelRowData(EmojiPanelRowType type, string headerTitle, int emojiStartIndex, int emojiCount)
        {
            Type = type;
            HeaderTitle = headerTitle;
            EmojiStartIndex = emojiStartIndex;
            EmojiCount = emojiCount;
        }

        public static EmojiPanelRowData Header(string title) =>
            new (EmojiPanelRowType.Header, title, 0, 0);

        public static EmojiPanelRowData EmojiRow(int emojiStartIndex, int emojiCount) =>
            new (EmojiPanelRowType.Emoji, string.Empty, emojiStartIndex, emojiCount);
    }

    public enum EmojiPanelRowType
    {
        Header,
        Emoji,
    }
}
