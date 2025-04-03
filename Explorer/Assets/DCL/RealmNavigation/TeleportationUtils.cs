using System;

namespace DCL.RealmNavigation
{
    public static class TeleportationUtils
    {
        private const string TRAM_LINE_TITLE = "Tram Line";

        public static bool IsTramLine(ReadOnlySpan<char> originalJson) =>
            ExtractTitleValue(originalJson).SequenceEqual(TRAM_LINE_TITLE.AsSpan());

        private static ReadOnlySpan<char> ExtractTitleValue(ReadOnlySpan<char> json)
        {
            int titleIndex = json.IndexOf(@"""title"":");

            if (titleIndex == -1)
                return ReadOnlySpan<char>.Empty;

            // Move to the start of the title value (after "title": ")
            int valueStartIndex = json[titleIndex..].IndexOf(':') + 1;
            ReadOnlySpan<char> valueSpan = json.Slice(titleIndex + valueStartIndex);

            int openQuoteIndex = valueSpan.IndexOf('"');

            if (openQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            int closeQuoteIndex = valueSpan[(openQuoteIndex + 1)..].IndexOf('"');

            if (closeQuoteIndex == -1)
                return ReadOnlySpan<char>.Empty;

            return valueSpan.Slice(openQuoteIndex + 1, closeQuoteIndex);
        }
    }
}
