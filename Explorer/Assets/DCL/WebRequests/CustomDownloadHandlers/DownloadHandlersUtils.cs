using System.Text;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class DownloadHandlersUtils
    {
        private static readonly StringBuilder STRING_BUILDER = new ();

        public static string GetContentRangeHeaderValue(long start, long end)
        {
            STRING_BUILDER.Clear();
            STRING_BUILDER.Append("bytes=");
            STRING_BUILDER.Append(start);
            STRING_BUILDER.Append("-");
            STRING_BUILDER.Append(end);
            return STRING_BUILDER.ToString();
        }

        public static bool TryGetFullSize(string input, out int result)
        {
            result = 0;

            if (string.IsNullOrEmpty(input)) return false;

            int separatorIndex = input.IndexOf('/');
            if (separatorIndex == -1 || separatorIndex == input.Length - 1) return false;

            string secondPart = input.Substring(separatorIndex + 1);
            return int.TryParse(secondPart, out result);
        }
    }
}
