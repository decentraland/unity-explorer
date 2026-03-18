namespace DCL.Chat.ChatReactions
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
    }
}
