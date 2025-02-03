using DCL.Emoji;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct EmojiInputSuggestionData : ISuggestionElementData
    {
        public readonly EmojiData EmojiData { get; }

        public EmojiInputSuggestionData(EmojiData emojiData)
        {
            EmojiData = emojiData;
        }

        public string GetId() =>
            EmojiData.EmojiCode;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.EMOJIS;
    }
}
