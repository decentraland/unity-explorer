using System;

namespace DCL.UI
{
    /// <summary>
    ///     Neutralizes TMP markup in strings authored by other users — profile and community names, announcement
    ///     bodies, event and place descriptions — before a rich-text label renders them. The characters TMP reads
    ///     as markup are swapped for lookalikes it does not, so an injected tag shows up as literal text instead
    ///     of being interpreted: a crafted name cannot spoof a verified badge, hide the copy around it with
    ///     <c>&lt;size=0&gt;</c>, or smuggle in a clickable <c>&lt;link&gt;</c> to an external URL.
    /// </summary>
    /// <remarks>
    ///     Escaping is preferred over turning <c>richText</c> off wherever the label's own copy is styled markup
    ///     that has to keep working; where the label renders nothing but the untrusted value, disabling
    ///     <c>richText</c> is simpler and equally safe.
    /// </remarks>
    public static class RichTextSanitizer
    {
        /// <summary>
        ///     Default cap for a single-line name authored by another user. Sits above the 45 characters this
        ///     client's own name field accepts (<c>NameInputFieldView.maxNameLength * 3</c>), so nothing a user can
        ///     legitimately pick is clipped, while a name padded out to thousands of characters cannot cost TMP an
        ///     unbounded layout pass. Nothing bounds a profile name upstream — the validator filters characters but
        ///     not length — so this is a display-side limit, not a mirror of a schema rule.
        /// </summary>
        public const int DEFAULT_NAME_LENGTH = 64;

        /// <summary>
        ///     Default cap for a multi-line body authored by another user — a description, an announcement, a
        ///     request message. Twice the 500 characters the announcement composer accepts, so a body written
        ///     through another client is not clipped, while a deliberately oversized one still costs a bounded
        ///     layout pass. Like <see cref="DEFAULT_NAME_LENGTH"/> this is a display-side limit; the services
        ///     that carry these fields do not agree on one.
        /// </summary>
        public const int DEFAULT_BODY_LENGTH = 1000;

        private const char LEFT_ANGLE_LOOKALIKE = '‹'; // Single left-pointing angle quotation mark.
        private const char RIGHT_ANGLE_LOOKALIKE = '›'; // Single right-pointing angle quotation mark.
        private const char QUOTE_LOOKALIKE = '”'; // Right double quotation mark.
        private const char BACKSLASH_LOOKALIKE = '＼'; // Fullwidth reverse solidus.
        private const char ELLIPSIS = '…';

        /// <summary>
        ///     Escapes a value that lands in content position — between tags, where it can only do harm by
        ///     opening one of its own. Straight quotes are left alone so ordinary prose survives intact.
        /// </summary>
        /// <remarks>
        ///     The backslash is escaped alongside the angle brackets, and dropping it reopens the hole it closes:
        ///     TMP rewrites a backslash-u escape sequence into the character it denotes, inside the very array its
        ///     tag parser then reads
        ///     (<c>TMP_Text.PopulateTextProcessingArray</c>, <c>case 117</c>), so a value carrying the escape
        ///     sequence rather than the character would sail through a brackets-only filter and still render as
        ///     live markup. That branch is gated by neither <c>parseCtrlCharacters</c> nor the input-source check
        ///     above it, which ships commented out — there is no label setting that turns it off.
        /// </remarks>
        /// <returns>The argument itself when it carries no markup, so the common case does not allocate.</returns>
        public static string Escape(string? value) =>
            Sanitize(value, escapeQuotes: false);

        /// <summary>
        ///     Escapes a value that is interpolated into a tag attribute, as in
        ///     <c>&lt;link="{value}"&gt;</c>. Adds the double quote that would otherwise close the attribute
        ///     early and let the rest of the value be read as further markup.
        /// </summary>
        public static string EscapeAttribute(string? value) =>
            Sanitize(value, escapeQuotes: true);

        /// <summary>
        ///     <see cref="Escape"/> plus a hard length cap, for the short single-line fields — names, titles —
        ///     where a long run of nested tags would otherwise cost TMP an unbounded layout pass.
        /// </summary>
        public static string EscapeAndTruncate(string? value, int maxLength)
        {
            if (value == null || value.Length <= maxLength)
                return Escape(value);

            // The cut, the swap and the ellipsis all land in the result's own buffer, so an oversized value
            // costs one allocation rather than a Substring, an escaped copy of it and a concatenation.
            return string.Create(CutLength(value, maxLength) + 1, value, static (destination, source) =>
            {
                int truncatedLength = destination.Length - 1;
                Neutralize(source.AsSpan(0, truncatedLength), destination, escapeQuotes: false);
                destination[truncatedLength] = ELLIPSIS;
            });
        }

        /// <summary>
        ///     The length cap on its own, for prose on a label whose <c>richText</c> is off in its prefab: nothing
        ///     there parses markup, so escaping would only cost fidelity — it would turn an honest "5 &lt; 10" into
        ///     "5 ‹ 10". Short name fields on such labels still prefer <see cref="EscapeAndTruncate"/>, where a
        ///     bracket is never legitimate anyway and the escape keeps them safe against a label that later has
        ///     rich text turned back on. Any label that renders rich text today must use it.
        /// </summary>
        public static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= maxLength)
                return value;

            return string.Create(CutLength(value, maxLength) + 1, value, static (destination, source) =>
            {
                int truncatedLength = destination.Length - 1;
                source.AsSpan(0, truncatedLength).CopyTo(destination);
                destination[truncatedLength] = ELLIPSIS;
            });
        }

        /// <summary>
        ///     Where to cut so the result never ends on half of a surrogate pair — an emoji split down the middle
        ///     renders as tofu rather than as the character the author wrote.
        /// </summary>
        private static int CutLength(string value, int maxLength) =>
            maxLength > 0 && char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;

        private static string Sanitize(string? value, bool escapeQuotes)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (IndexOfMarkup(value, escapeQuotes) < 0)
                return value;

            // One allocation: the swapped characters are written straight into the new string's buffer,
            // instead of through an intermediate char[].
            return string.Create(value.Length, (value, escapeQuotes), static (destination, state) =>
                Neutralize(state.value.AsSpan(), destination, state.escapeQuotes));
        }

        private static void Neutralize(ReadOnlySpan<char> source, Span<char> destination, bool escapeQuotes)
        {
            for (var i = 0; i < source.Length; i++)
            {
                char character = source[i];

                destination[i] = character switch
                                 {
                                     '<' => LEFT_ANGLE_LOOKALIKE,
                                     '>' => RIGHT_ANGLE_LOOKALIKE,
                                     '\\' => BACKSLASH_LOOKALIKE,
                                     '"' when escapeQuotes => QUOTE_LOOKALIKE,
                                     _ => character,
                                 };
            }
        }

        private static int IndexOfMarkup(string value, bool escapeQuotes)
        {
            for (var i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (character is '<' or '>' or '\\' || (escapeQuotes && character == '"'))
                    return i;
            }

            return -1;
        }
    }
}
