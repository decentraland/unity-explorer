using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        ///     A request matching none of the mapped shapes. It names neither a recipient nor an amount,
        ///     so the raw payload is what the user reviews.
        /// </summary>
        Unknown,
    }

    public readonly struct DecodedTransaction
    {
        public readonly TransactionKind Kind;

        /// <summary>
        ///     The address that receives the assets: the `to` field for native transfers, or the address
        ///     decoded from the calldata for ERC-20 transfers. Empty for <see cref="TransactionKind.Unknown" />.
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
    ///     Maps a scene web3 request onto one of the transfer shapes this client can summarize: a native
    ///     transfer, an ERC-20 transfer (where `to` is the token contract and the recipient sits in the
    ///     calldata), or the ERC-20 transfer carried in the EIP-712 payload of a meta-transaction
    ///     signature. Everything else is <see cref="TransactionKind.Unknown" />.
    /// </summary>
    public static class TransactionRecipientDecoder
    {
        // keccak256("transfer(address,uint256)")[0..4]
        private const string TRANSFER_SELECTOR = "a9059cbb";

        // EIP-712 primary type of the Polygon native meta-transaction.
        private const string META_TRANSACTION_TYPE = "MetaTransaction";

        private const int SELECTOR_LENGTH = 8; // 4 bytes
        private const int WORD_LENGTH = 64; // 32 bytes
        private const int ADDRESS_LENGTH = 40; // 20 bytes
        private const int ERC20_TRANSFER_LENGTH = SELECTOR_LENGTH + (WORD_LENGTH * 2);

        private static DecodedTransaction Unknown => new (TransactionKind.Unknown, string.Empty, BigInteger.Zero, null);

        public static DecodedTransaction Decode(string? to, string? value, string? data)
        {
            string toAddress = to ?? string.Empty;
            string cleanData = StripHexPrefix(data);

            // No calldata: a plain native currency transfer, which needs a destination to be one at all.
            if (cleanData.Length == 0)
                return toAddress.Length > 0
                    ? new DecodedTransaction(TransactionKind.NativeTransfer, toAddress, ParseHex(value), null)
                    : Unknown;

            // ERC-20 transfer(address,uint256): recipient and amount come from the calldata.
            if (cleanData.Length >= ERC20_TRANSFER_LENGTH
                && cleanData.StartsWith(TRANSFER_SELECTOR, StringComparison.OrdinalIgnoreCase))
            {
                string recipientWord = cleanData.Substring(SELECTOR_LENGTH, WORD_LENGTH);
                string recipient = "0x" + recipientWord.Substring(WORD_LENGTH - ADDRESS_LENGTH);
                BigInteger amount = ParseHex(cleanData.Substring(SELECTOR_LENGTH + WORD_LENGTH, WORD_LENGTH));
                return new DecodedTransaction(TransactionKind.Erc20Transfer, recipient, amount, toAddress);
            }

            // Opaque contract call: the contract it targets is not a recipient.
            return Unknown;
        }

        /// <summary>
        ///     Decodes the ERC-20 transfer authorized by a meta-transaction signature: the call is in
        ///     `message.functionSignature`, the token contract in the domain's `verifyingContract`.
        ///     False for any other payload, since the contract of an opaque call is not a recipient.
        /// </summary>
        public static bool TryDecodeMetaTransaction(string? typedDataJson, out DecodedTransaction decoded)
        {
            decoded = Unknown;

            if (string.IsNullOrEmpty(typedDataJson))
                return false;

            try
            {
                var typedData = JObject.Parse(typedDataJson!);

                if (!string.Equals(typedData["primaryType"]?.ToString(), META_TRANSACTION_TYPE, StringComparison.Ordinal))
                    return false;

                string? functionSignature = (typedData["message"] as JObject)?["functionSignature"]?.ToString();
                string? tokenContract = (typedData["domain"] as JObject)?["verifyingContract"]?.ToString();

                DecodedTransaction call = Decode(tokenContract, null, functionSignature);

                if (call.Kind != TransactionKind.Erc20Transfer)
                    return false;

                decoded = call;
                return true;
            }
            // A malformed payload from the scene is undecodable, not an error.
            catch (JsonException) { return false; }
            catch (FormatException) { return false; }
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
