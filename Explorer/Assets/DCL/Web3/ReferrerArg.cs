using System.Text.RegularExpressions;

namespace DCL.Web3
{
    /// <summary>
    ///     Validates the referral attribution address received via the launcher's
    ///     <c>--referrer</c> argument (or embedded in a deep link). The value is
    ///     untrusted input: it is only ever used when it matches an Ethereum
    ///     address exactly.
    /// </summary>
    public static class ReferrerArg
    {
        private static readonly Regex ADDRESS_REGEX = new ("^0x[a-fA-F0-9]{40}$", RegexOptions.Compiled);

        /// <summary>
        ///     Returns the lowercased address when valid, null otherwise.
        /// </summary>
        public static string? Normalize(string? referrer) =>
            referrer != null && ADDRESS_REGEX.IsMatch(referrer) ? referrer.ToLowerInvariant() : null;
    }
}
