using System;
using System.Text;

namespace DCL.WebRequests
{
    public static class DownloadHandlersUtils
    {
        private const string BYTES_HEADER = "bytes=";
        private const string BYTES_HEADER_SEPARATOR = "-";
        private static readonly StringBuilder STRING_BUILDER = new ();

        public static string GetContentRangeHeaderValue(long start, long end)
        {
            STRING_BUILDER.Clear();
            STRING_BUILDER.Append(BYTES_HEADER);
            STRING_BUILDER.Append(start);
            STRING_BUILDER.Append(BYTES_HEADER_SEPARATOR);
            STRING_BUILDER.Append(end);
            return STRING_BUILDER.ToString();
        }

        public static bool TryParseContentRange(string? input, out long fullSize, out long chunkSize)
        {
            fullSize = 0;
            chunkSize = 0;

            if (string.IsNullOrEmpty(input)) return false;

            ReadOnlySpan<char> span = input.AsSpan();

            // Parse the format like "bytes 0-300/723462"

            const string BYTES = "bytes ";

            if (span.Length < BYTES.Length || !span.StartsWith(BYTES)) return false;
            span = span.Slice(BYTES.Length);

            int separatorIndex = span.IndexOf('/');
            if (separatorIndex == -1 || separatorIndex == input.Length - 1) return false;

            ReadOnlySpan<char> firstPart = span.Slice(0, separatorIndex);

            // Parse the first part like "0-300"
            int dashIndex = firstPart.IndexOf('-');
            if (dashIndex == -1 || dashIndex == 0 || dashIndex == firstPart.Length - 1) return false;

            ReadOnlySpan<char> startPart = firstPart.Slice(0, dashIndex);
            ReadOnlySpan<char> endPart = firstPart.Slice(dashIndex + 1);
            if (!long.TryParse(startPart, out long start) || !long.TryParse(endPart, out long end)) return false;
            if (start > end) return false;
            chunkSize = end - start + 1;

            ReadOnlySpan<char> secondPart = span.Slice(separatorIndex + 1);
            return long.TryParse(secondPart, out fullSize);
        }
    }
}
