using System;
using System.Globalization;
using System.Numerics;

namespace DCL.Web3
{
    public enum TransactionKind
    {
        /// <summary>
        ///     Native currency transfer (ETH/MATIC): a value transfer with no calldata.
        /// </summary>
        NativeTransfer,

        /// <summary>
        ///     ERC-20 transfer(address,uint256) call.
        /// </summary>
        Erc20Transfer,

        /// <summary>
        ///     Any other contract call, which cannot be decoded into a plain transfer.
        /// </summary>
        ContractCall,
    }

    public readonly struct DecodedTransaction
    {
        public readonly TransactionKind Kind;

        /// <summary>
        ///     The address that receives the assets: the `to` field for native transfers and opaque
        ///     contract calls, or the address decoded from the calldata for ERC-20 transfers.
        /// </summary>
        public readonly string Recipient;

        /// <summary>
        ///     Amount in the smallest unit (wei for native transfers, token base units for ERC-20).
        ///     Zero when it cannot be determined.
        /// </summary>
        public readonly BigInteger Amount;

        /// <summary>
        ///     The token contract of an ERC-20 transfer; null otherwise.
        /// </summary>
        public readonly string? TokenContract;

        public DecodedTransaction(TransactionKind kind, string recipient, BigInteger amount, string? tokenContract)
        {
            Kind = kind;
            Recipient = recipient;
            Amount = amount;
            TokenContract = tokenContract;
        }
    }

    /// <summary>
    ///     Resolves who actually receives the assets of an eth_sendTransaction. A native transfer sends to
    ///     the `to` field, but an ERC-20 transfer sends to an address encoded in the calldata while `to`
    ///     holds the token contract.
    /// </summary>
    public static class TransactionRecipientDecoder
    {
        // keccak256("transfer(address,uint256)")[0..4]
        private const string TRANSFER_SELECTOR = "a9059cbb";

        private const int SELECTOR_LENGTH = 8; // 4 bytes
        private const int WORD_LENGTH = 64; // 32 bytes
        private const int ADDRESS_LENGTH = 40; // 20 bytes
        private const int ERC20_TRANSFER_LENGTH = SELECTOR_LENGTH + (WORD_LENGTH * 2);

        public static DecodedTransaction Decode(string? to, string? value, string? data)
        {
            string toAddress = to ?? string.Empty;
            string cleanData = StripHexPrefix(data);

            // No calldata: a plain native currency transfer.
            if (cleanData.Length == 0)
                return new DecodedTransaction(TransactionKind.NativeTransfer, toAddress, ParseHex(value), null);

            // ERC-20 transfer(address,uint256): recipient and amount come from the calldata.
            if (cleanData.Length >= ERC20_TRANSFER_LENGTH
                && cleanData.StartsWith(TRANSFER_SELECTOR, StringComparison.OrdinalIgnoreCase))
            {
                string recipientWord = cleanData.Substring(SELECTOR_LENGTH, WORD_LENGTH);
                string recipient = "0x" + recipientWord.Substring(WORD_LENGTH - ADDRESS_LENGTH);
                BigInteger amount = ParseHex(cleanData.Substring(SELECTOR_LENGTH + WORD_LENGTH, WORD_LENGTH));
                return new DecodedTransaction(TransactionKind.Erc20Transfer, recipient, amount, toAddress);
            }

            // Opaque contract call: the contract itself is the closest thing to a recipient.
            return new DecodedTransaction(TransactionKind.ContractCall, toAddress, ParseHex(value), null);
        }

        private static string StripHexPrefix(string? hex)
        {
            if (string.IsNullOrEmpty(hex))
                return string.Empty;

            return hex!.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        }

        private static BigInteger ParseHex(string? hexValue)
        {
            string clean = StripHexPrefix(hexValue);

            if (clean.Length == 0)
                return BigInteger.Zero;

            // Prefix '0' so the leading bit is never interpreted as a sign.
            return BigInteger.Parse("0" + clean, NumberStyles.HexNumber);
        }
    }
}
