using System;
using System.Text;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public static class DownloadHandlersUtils
    {
        private static readonly StringBuilder STRING_BUILDER = new ();
        private const string BYTES_HEADER = "bytes=";
        private const string BYTES_HEADER_SEPARATOR = "-";

        public static string GetContentRangeHeaderValue(long start, long end)
        {
            STRING_BUILDER.Clear();
            STRING_BUILDER.Append(BYTES_HEADER);
            STRING_BUILDER.Append(start);
            STRING_BUILDER.Append(BYTES_HEADER_SEPARATOR);
            STRING_BUILDER.Append(end);
            return STRING_BUILDER.ToString();
        }

        public static bool TryGetFullSize(string input, out int result)
        {
            result = 0;

            if (string.IsNullOrEmpty(input)) return false;

            int separatorIndex = input.IndexOf('/');
            if (separatorIndex == -1 || separatorIndex == input.Length - 1) return false;

            ReadOnlySpan<char> secondPart = input.AsSpan(separatorIndex + 1);
            return int.TryParse(secondPart, out result);
        }
    }
}
