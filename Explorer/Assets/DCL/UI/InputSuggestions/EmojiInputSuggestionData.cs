namespace DCL.UI.SuggestionPanel
{
    public readonly struct EmojiInputSuggestionData : ISuggestionElementData
    {
        public readonly string EmojiCode;
        public readonly string EmojiName;

        public EmojiInputSuggestionData(string emojiCode, string emojiName)
        {
            EmojiCode = emojiCode;
            EmojiName = emojiName;
        }

        public string GetId() =>
            EmojiCode;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.EMOJIS;
    }
}
