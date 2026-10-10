using DCL.Web3;
using NUnit.Framework;
using System.Numerics;

namespace DCL.EditModeTests
{
    [TestFixture]
    public class DecodedTransactionShould
    {
        private const string RECIPIENT = "0x430637b3f9c6d36e25f8221b6531390f777e433f";
        private const string MANA_CONTRACT = "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4";

        // 5 * 10^18 in hex, left-padded to 32 bytes
        private const string FIVE_TOKENS_WORD = "0000000000000000000000000000000000000000000000004563918244f40000";
        private const string RECIPIENT_WORD = "000000000000000000000000430637b3f9c6d36e25f8221b6531390f777e433f";

        private static readonly BigInteger FIVE_TOKENS = BigInteger.Parse("5000000000000000000");

        [Test]
        public void DecodeNativeTransferWhenNoData()
        {
            DecodedTransaction result = DecodedTransaction.From(RECIPIENT, "0x4563918244f40000", null);

            Assert.AreEqual(TransactionKind.NativeTransfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(FIVE_TOKENS, result.Amount);
            Assert.IsNull(result.TokenContract);
        }

        [Test]
        public void DecodeNativeTransferWhenDataIsEmptyHex()
        {
            DecodedTransaction result = DecodedTransaction.From(RECIPIENT, "0x0", "0x");

            Assert.AreEqual(TransactionKind.NativeTransfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(BigInteger.Zero, result.Amount);
        }

        [Test]
        public void DecodeErc20TransferRecipientFromCalldataNotToField()
        {
            string data = "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Erc20Transfer, result.Kind);
            // The recipient is the address encoded in the calldata, not the token contract in `to`.
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(FIVE_TOKENS, result.Amount);
            Assert.AreEqual(MANA_CONTRACT, result.TokenContract);
        }

        [Test]
        public void DecodeErc20TransferWithMixedCaseSelector()
        {
            string data = "0xA9059CBB" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Erc20Transfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
        }

        [Test]
        public void TreatUnknownSelectorAsUnknown()
        {
            string data = "0x12345678" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
            Assert.IsEmpty(result.Recipient);
            Assert.AreEqual(BigInteger.Zero, result.Amount);
            Assert.IsNull(result.TokenContract);
        }

        [Test]
        public void TreatTruncatedTransferCalldataAsUnknown()
        {
            // transfer selector but missing the amount word.
            string data = "0xa9059cbb" + RECIPIENT_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
            Assert.IsEmpty(result.Recipient);
        }

        [Test]
        public void TreatTransferCalldataCarryingNativeValueAsUnknown()
        {
            // Summarizing this as an ERC-20 transfer would never mention the native currency riding along.
            string data = "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x4563918244f40000", data);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
            Assert.IsEmpty(result.Recipient);
        }

        [Test]
        public void TreatTransferCalldataWithTrailingArgumentsAsUnknown()
        {
            // The two words the copy describes, plus one it does not.
            string data = "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD + RECIPIENT_WORD;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
        }

        [Test]
        public void StillDecodeATransferWhoseValueIsExplicitlyZero()
        {
            string data = "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            // A zero value arrives as "0x0" or null depending on the caller; neither moves native currency.
            foreach (string? zero in new[] { "0x0", "0x00", null })
            {
                DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, zero, data);

                Assert.AreEqual(TransactionKind.Erc20Transfer, result.Kind, $"value {zero ?? "null"}");
                Assert.AreEqual(RECIPIENT, result.Recipient);
            }
        }

        [Test]
        public void TreatValueTransferWithoutDestinationAsUnknown()
        {
            DecodedTransaction result = DecodedTransaction.From(null, "0x4563918244f40000", null);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
            Assert.IsEmpty(result.Recipient);
        }

        [Test]
        public void ParseLargeAmountWordAsPositive()
        {
            // A word with the high bit set must not be interpreted as a negative number.
            string maxWord = new string('f', 64);
            string data = "0xa9059cbb" + RECIPIENT_WORD + maxWord;

            DecodedTransaction result = DecodedTransaction.From(MANA_CONTRACT, "0x0", data);

            Assert.IsTrue(result.Amount > BigInteger.Zero);
        }

        [Test]
        public void DecodeMetaTransactionTransferFromTypedData()
        {
            string typedData = MetaTransactionTypedData(MANA_CONTRACT, "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD);

            Assert.IsTrue(DecodedTransaction.TryFromMetaTransaction(typedData, out DecodedTransaction result));
            Assert.AreEqual(TransactionKind.Erc20Transfer, result.Kind);
            // The recipient is the address inside the authorized call, not the verifying contract.
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(FIVE_TOKENS, result.Amount);
            Assert.AreEqual(MANA_CONTRACT, result.TokenContract);
        }

        [Test]
        public void RejectTypedDataOfAnotherPrimaryType()
        {
            string typedData = MetaTransactionTypedData(MANA_CONTRACT, "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD)
                .Replace("MetaTransaction", "Permit");

            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction(typedData, out DecodedTransaction _));
        }

        [Test]
        public void RejectMetaTransactionAuthorizingAnOpaqueCall()
        {
            string typedData = MetaTransactionTypedData(MANA_CONTRACT, "0x12345678" + RECIPIENT_WORD + FIVE_TOKENS_WORD);

            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction(typedData, out DecodedTransaction _));
        }

        [Test]
        public void RejectMetaTransactionWithoutFunctionSignature()
        {
            const string TYPED_DATA = "{\"primaryType\":\"MetaTransaction\",\"domain\":{\"verifyingContract\":\"" + MANA_CONTRACT + "\"},\"message\":{\"nonce\":0}}";

            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction(TYPED_DATA, out DecodedTransaction _));
        }

        [Test]
        public void RejectMalformedTypedData()
        {
            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction("not json", out DecodedTransaction _));
            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction(null, out DecodedTransaction _));
            // A message that is a string instead of an object must not throw.
            Assert.IsFalse(DecodedTransaction.TryFromMetaTransaction("{\"primaryType\":\"MetaTransaction\",\"message\":\"nope\"}", out DecodedTransaction _));
        }

        [Test]
        public void ReportUnknownWhenMetaTransactionIsRejected()
        {
            DecodedTransaction.TryFromMetaTransaction("not json", out DecodedTransaction result);

            Assert.AreEqual(TransactionKind.Unknown, result.Kind);
            Assert.IsEmpty(result.Recipient);
        }

        // EIP-712 payload of a Polygon native meta-transaction, as a scene sends it.
        private static string MetaTransactionTypedData(string verifyingContract, string functionSignature) =>
            "{\"types\":{\"MetaTransaction\":[{\"name\":\"nonce\",\"type\":\"uint256\"},{\"name\":\"from\",\"type\":\"address\"},{\"name\":\"functionSignature\",\"type\":\"bytes\"}]},"
            + $"\"domain\":{{\"name\":\"Decentraland MANA\",\"version\":\"1\",\"verifyingContract\":\"{verifyingContract}\",\"salt\":\"0x0000000000000000000000000000000000000000000000000000000000013881\"}},"
            + "\"primaryType\":\"MetaTransaction\","
            + $"\"message\":{{\"nonce\":0,\"from\":\"{RECIPIENT}\",\"functionSignature\":\"{functionSignature}\"}}}}";
    }
}
