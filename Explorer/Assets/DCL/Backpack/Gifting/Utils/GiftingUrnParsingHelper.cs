using System;

namespace DCL.Backpack.Gifting.Utils
{
    public static class GiftingUrnParsingHelper
    {
        /// <summary>
        ///     Extracts the Base URN (everything before the Token ID) from a full URN.
        ///     Expected format: urn:chain:contract:tokenId
        /// </summary>
        public static bool TryGetBaseUrn(string fullUrn, out string baseUrn)
        {
            baseUrn = string.Empty;

            if (string.IsNullOrEmpty(fullUrn))
                return false;

            var span = fullUrn.AsSpan();

            // We are looking for the last ':' which separates the TokenID from the rest
            int lastColonIndex = span.LastIndexOf(':');

            // Validation: 
            // 1. Colon must exist
            // 2. Colon cannot be at start (index 0)
            // 3. Colon cannot be at the very end (meaning empty TokenID)
            if (lastColonIndex <= 0 || lastColonIndex >= span.Length - 1)
                return false;

            // Slice the span to get everything before the colon
            baseUrn = span.Slice(0, lastColonIndex).ToString();
            return true;
        }

        /// <summary>
        ///     Extracts the Contract Address (starts with 0x) from a URN.
        /// </summary>
        public static bool TryGetContractAddress(string? urn, out string contractAddress)
        {
            contractAddress = string.Empty;

            if (string.IsNullOrEmpty(urn))
                return false;

            // Efficient Span-based search for "0x"
            var span = urn.AsSpan();
            int hexIndex = span.IndexOf("0x".AsSpan(), StringComparison.OrdinalIgnoreCase);

            // Not found
            if (hexIndex == -1)
                return false;

            // A standard Ethereum address is 42 chars (0x + 40 hex digits)
            if (span.Length < hexIndex + 42)
                return false;

            // Slice exactly 42 characters
            contractAddress = span.Slice(hexIndex, 42).ToString();
            return true;
        }
    }
}