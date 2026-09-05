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
        private const string MANA_BALANCE_FUNCTION_SELECTOR = "70a08231";
        private const string TRANSFER_FUNCTION_SELECTOR = "a9059cbb";

        private const string HEX_PREFIX = "0x";

        private const decimal WEI_FACTOR = 1_000_000_000_000_000_000;

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
            return string.Concat(HEX_PREFIX, TRANSFER_FROM_SELECTOR, paddedFrom, paddedTo, paddedId);
        }

        public static string EncodeGetBalance(string walletAddress)
        {
            string address = LeftPad64(NormalizeAddress(walletAddress));

            return string.Concat(HEX_PREFIX, MANA_BALANCE_FUNCTION_SELECTOR, address);
        }

        public static string EncodeSendDonation(string toAddress, decimal amountInMana)
        {
            BigInteger value = new BigInteger(decimal.Round(amountInMana * WEI_FACTOR, 0, MidpointRounding.AwayFromZero));
            string to = LeftPad64(NormalizeAddress(toAddress));
            string weiAmountString = LeftPad64(value.ToString("x"));

            return string.Concat(HEX_PREFIX, TRANSFER_FUNCTION_SELECTOR, to, weiAmountString);
        }

        private static string NormalizeAddress(string addr)
        {
            if (addr.Length != 40 && addr.Length != 42)
                throw new ArgumentException($"Invalid address length: {addr.Length}. Expected 40 or 42 (could start with `0x`) hex characters.");

            // Remove '0x' prefix if present
            string s = addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr;

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
