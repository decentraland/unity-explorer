using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Numerics;

namespace DCL.Web3
{
    public enum TransactionKind
    {
        NativeTransfer,
        Erc20Transfer,

        /// <summary>
        ///     Matches none of the mapped shapes, so the raw payload is what the user reviews.
        /// </summary>
        Unknown,
    }

    /// <summary>
    ///     A scene web3 request mapped onto a transfer shape this client can summarize: a native transfer,
    ///     an ERC-20 transfer, or the ERC-20 transfer carried in a meta-transaction signature.
    /// </summary>
    public readonly struct DecodedTransaction
    {
        // keccak256("transfer(address,uint256)")[0..4]
        private const string TRANSFER_SELECTOR = "a9059cbb";

        private const string META_TRANSACTION_TYPE = "MetaTransaction";

        private const int SELECTOR_LENGTH = 8; // 4 bytes
        private const int WORD_LENGTH = 64; // 32 bytes
        private const int ADDRESS_LENGTH = 40; // 20 bytes
        private const int ERC20_TRANSFER_LENGTH = SELECTOR_LENGTH + (WORD_LENGTH * 2);

        private static readonly DecodedTransaction UNKNOWN = new (TransactionKind.Unknown, string.Empty, BigInteger.Zero, null);

        public readonly TransactionKind Kind;

        /// <summary>
        ///     For an ERC-20 transfer this is the address inside the calldata, not `to`. Empty for
        ///     <see cref="TransactionKind.Unknown" />.
        /// </summary>
        public readonly string Recipient;

        /// <summary>
        ///     Smallest unit: wei for native transfers, token base units for ERC-20. Zero when unknown.
        /// </summary>
        public readonly BigInteger Amount;

        public readonly string? TokenContract;

        internal DecodedTransaction(TransactionKind kind, string recipient, BigInteger amount, string? tokenContract)
        {
            Kind = kind;
            Recipient = recipient;
            Amount = amount;
            TokenContract = tokenContract;
        }

        public static DecodedTransaction From(string? to, string? value, string? data)
        {
            string toAddress = to ?? string.Empty;
            string cleanData = StripHexPrefix(data);

            // A native transfer needs a destination to be one at all.
            if (cleanData.Length == 0)
                return toAddress.Length > 0
                    ? new DecodedTransaction(TransactionKind.NativeTransfer, toAddress, ParseHex(value), null)
                    : UNKNOWN;

            // Only the exact shape counts: a summary of this call states the token amount and nothing
            // else, so native value riding along, or arguments past the two described, would move assets
            // the copy never mentions.
            if (cleanData.Length == ERC20_TRANSFER_LENGTH
                && ParseHex(value).IsZero
                && cleanData.StartsWith(TRANSFER_SELECTOR, StringComparison.OrdinalIgnoreCase))
            {
                string recipientWord = cleanData.Substring(SELECTOR_LENGTH, WORD_LENGTH);
                string recipient = "0x" + recipientWord.Substring(WORD_LENGTH - ADDRESS_LENGTH);
                BigInteger amount = ParseHex(cleanData.Substring(SELECTOR_LENGTH + WORD_LENGTH, WORD_LENGTH));
                return new DecodedTransaction(TransactionKind.Erc20Transfer, recipient, amount, toAddress);
            }

            // The contract an opaque call targets is not a recipient.
            return UNKNOWN;
        }

        /// <summary>
        ///     The authorized call is in `message.functionSignature`, the token contract in the domain's
        ///     `verifyingContract`.
        /// </summary>
        public static bool TryFromMetaTransaction(string? typedDataJson, out DecodedTransaction decoded)
        {
            decoded = UNKNOWN;

            if (string.IsNullOrEmpty(typedDataJson))
                return false;

            try
            {
                var typedData = JObject.Parse(typedDataJson);

                if (!string.Equals(typedData["primaryType"]?.ToString(), META_TRANSACTION_TYPE, StringComparison.Ordinal))
                    return false;

                string? functionSignature = (typedData["message"] as JObject)?["functionSignature"]?.ToString();
                string? tokenContract = (typedData["domain"] as JObject)?["verifyingContract"]?.ToString();

                DecodedTransaction call = From(tokenContract, null, functionSignature);

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

            return hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
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
