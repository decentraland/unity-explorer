namespace DCL.Emoji
{
    public struct EmojiData
    {
        public readonly string EmojiCode;
        public readonly string EmojiName;

        public EmojiData(string emojiCode, string emojiName)
        {
            EmojiCode = emojiCode;
            EmojiName = emojiName;
        }
    }
}
