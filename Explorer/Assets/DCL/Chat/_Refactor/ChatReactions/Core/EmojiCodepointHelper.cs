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
