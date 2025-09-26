using System.Text.RegularExpressions;

namespace DCL.Translation.Processors
{
    public static class ProtectedPatterns
    {
        internal static readonly Regex CurrencyAmountRx = new(
            @"(?<!\p{L})(?:[$€£¥₩₽₹]|USD|EUR|GBP|JPY|KRW|RUB|INR)\s?" +
            @"(?:[0-9]{1,3}(?:[.,\u202F\u00A0][0-9]{3})*|[0-9]+)(?:[.,][0-9]{1,2})?(?!\p{L})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex AmountCurrencyRx = new(
            @"(?<!\p{L})(?:[0-9]{1,3}(?:[.,\u202F\u00A0][0-9]{3})*|[0-9]+)(?:[.,][0-9]{1,2})?\s?" +
            @"(?:[$€£¥₩₽₹]|USD|EUR|GBP|JPY|KRW|RUB|INR)(?!\p{L})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // TIME (24h and 12h formats)
        internal static readonly Regex Time24Rx = new(
            @"\b(?:[01]?\d|2[0-3])[:.][0-5]\d(?:[:.][0-5]\d)?\b",
            RegexOptions.Compiled);

        internal static readonly Regex Time12Rx = new(
            @"\b(?:0?[1-9]|1[0-2])[:.][0-5]\d(?:[:.][0-5]\d)?\s?(?:[APap][Mm])\b",
            RegexOptions.Compiled);

        // DATES (ISO, slash, dot formats)
        internal static readonly Regex IsoDateRx =
            new(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.Compiled);

        internal static readonly Regex SlashDateRx =
            new(@"\b[0-3]?\d/[01]?\d/\d{4}\b", RegexOptions.Compiled);

        internal static readonly Regex DotDateRx =
            new(@"\b[0-3]?\d\.[01]?\d\.\d{4}\b", RegexOptions.Compiled);

        /// <summary>
        ///     Matches a message that is ONLY a slash command (e.g., "/help").
        ///     This is used to skip translation for the entire message.
        /// </summary>
        internal static readonly Regex FullLineSlashCommandRx =
            new(@"^\s*/[A-Za-z][\w-]*(?:\s+.*)?$", RegexOptions.Compiled);

        /// <summary>
        ///     Matches an inline command (e.g., "go here /goto 10,20").
        ///     This is used to detect if a message requires complex processing to protect the command.
        /// </summary>
        internal static readonly Regex InlineCommandRx =
            new(@"(?<=^|\s)[/\\][A-Za-z][\w-]*\b", RegexOptions.Compiled);

        /// <summary>
        ///     Checks if a string contains any numeric, currency, date, or time patterns
        ///     that should be preserved during translation.
        /// </summary>
        public static bool HasProtectedNumericOrTemporal(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            return CurrencyAmountRx.IsMatch(s) || AmountCurrencyRx.IsMatch(s) ||
                   IsoDateRx.IsMatch(s) || SlashDateRx.IsMatch(s) || DotDateRx.IsMatch(s) ||
                   Time24Rx.IsMatch(s) || Time12Rx.IsMatch(s);
        }
    }
}