using System;
using System.Numerics;

namespace DCL.Backpack.Gifting.Utils
{
    public static class ManualTxEncoder
    {
        /// <summary>
        ///     The 4-byte function selector for ERC-721 'transferFrom(address,address,uint256)'.
        ///     Calculated as: keccak256("transferFrom(address,address,uint256)").Substring(0, 8)
        /// </summary>
        private const string TRANSFER_FROM_SELECTOR = "23b872dd";

        public static string EncodeTransferFrom(string fromAddress, string toAddress, string tokenIdDecimal)
        {
            // 1. Normalize Addresses (remove 0x, lowercase)
            string cleanFrom = NormalizeAddress(fromAddress);
            string cleanTo   = NormalizeAddress(toAddress);

            // 2. Convert Token ID (Decimal String -> BigInt -> Hex String)
            // Note: We use BigInteger because Token IDs can be larger than long/int64
            string hexTokenId = BigInteger.Parse(tokenIdDecimal).ToString("x");

            // 3. Pad arguments to 32 bytes (64 hex characters)
            // EVM expects every argument to be exactly 32 bytes wide.
            string paddedFrom = LeftPad64(cleanFrom);
            string paddedTo   = LeftPad64(cleanTo);
            string paddedId   = LeftPad64(hexTokenId);

            // 4. Construct Payload
            // 0x + [Selector] + [From] + [To] + [TokenID]
            return "0x" + TRANSFER_FROM_SELECTOR + paddedFrom + paddedTo + paddedId;
        }

        private static string NormalizeAddress(string addr)
        {
            // Remove '0x' prefix if present
            string s = addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr;

            if (s.Length != 40)
                throw new ArgumentException($"Invalid address length: {s.Length}. Expected 40 hex characters.");
            
            return s.ToLowerInvariant();
        }

        private static string LeftPad64(string hex)
        {
            // Ensure no 0x prefix before padding
            string s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (s.Length > 64)
                throw new ArgumentException($"Argument too large: {s.Length} chars. Max 64 hex chars allowed.");

            // Pad with zeros on the left to reach 64 characters
            return s.PadLeft(64, '0').ToLowerInvariant();
        }
    }
}