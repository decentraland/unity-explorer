namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Extracts a single Unicode codepoint from an emoji string.
    /// Handles both BMP characters and surrogate pairs.
    /// </summary>
    public static class EmojiCodepointHelper
    {
        public static bool TryGetSingleCodepoint(string text, out uint codepoint)
        {
            codepoint = 0;

            if (text.Length == 0)
                return false;

            if (char.IsHighSurrogate(text[0]))
            {
                if (text.Length < 2 || !char.IsLowSurrogate(text[1]))
                    return false;

                codepoint = (uint)char.ConvertToUtf32(text[0], text[1]);
            }
            else
            {
                codepoint = text[0];
            }

            return true;
        }

        public const uint REGIONAL_INDICATOR_START = 0x1F1E6;
        public const uint REGIONAL_INDICATOR_END = 0x1F1FF;
        public const int REGIONAL_INDICATOR_COUNT = 26;

        private static readonly string[] LetterShortcodes = BuildLetterShortcodes();

        private static string[] BuildLetterShortcodes()
        {
            var arr = new string[REGIONAL_INDICATOR_COUNT];

            for (int i = 0; i < REGIONAL_INDICATOR_COUNT; i++)
                arr[i] = $":letter-{(char)('A' + i)}:";

            return arr;
        }

        /// <summary>
        /// Regional indicator symbols (U+1F1E6–U+1F1FF) render as boxed letters A–Z,
        /// but the emoji panel maps them to flag shortcodes (flags are pairs of these).
        /// Returns the correct letter shortcode, or null if outside the range.
        /// </summary>
        public static string? TryGetRegionalIndicatorShortcode(uint unicode)
        {
            if (unicode < REGIONAL_INDICATOR_START || unicode > REGIONAL_INDICATOR_END) return null;
            return LetterShortcodes[unicode - REGIONAL_INDICATOR_START];
        }

        /// <summary>
        /// Converts a Unicode codepoint to its display string.
        /// Returns "?" for surrogates and out-of-range values.
        /// </summary>
        public static string CodepointToDisplayString(uint unicode)
        {
            return unicode switch
            {
                >= 0xD800 and <= 0xDFFF or > 0x10FFFF => "?",
                <= 0xFFFF => ((char)unicode).ToString(),
                _ => char.ConvertFromUtf32((int)unicode)
            };
        }
    }
}
