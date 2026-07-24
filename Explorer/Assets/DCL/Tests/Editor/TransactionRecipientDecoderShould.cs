using DCL.Web3;
using NUnit.Framework;
using System.Numerics;

namespace DCL.EditModeTests
{
    [TestFixture]
    public class TransactionRecipientDecoderShould
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
            DecodedTransaction result = TransactionRecipientDecoder.Decode(RECIPIENT, "0x4563918244f40000", null);

            Assert.AreEqual(TransactionKind.NativeTransfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(FIVE_TOKENS, result.Amount);
            Assert.IsNull(result.TokenContract);
        }

        [Test]
        public void DecodeNativeTransferWhenDataIsEmptyHex()
        {
            DecodedTransaction result = TransactionRecipientDecoder.Decode(RECIPIENT, "0x0", "0x");

            Assert.AreEqual(TransactionKind.NativeTransfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
            Assert.AreEqual(BigInteger.Zero, result.Amount);
        }

        [Test]
        public void DecodeErc20TransferRecipientFromCalldataNotToField()
        {
            string data = "0xa9059cbb" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = TransactionRecipientDecoder.Decode(MANA_CONTRACT, "0x0", data);

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

            DecodedTransaction result = TransactionRecipientDecoder.Decode(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.Erc20Transfer, result.Kind);
            Assert.AreEqual(RECIPIENT, result.Recipient);
        }

        [Test]
        public void TreatUnknownSelectorAsContractCall()
        {
            // A non-transfer selector with otherwise well-formed argument words.
            string data = "0x12345678" + RECIPIENT_WORD + FIVE_TOKENS_WORD;

            DecodedTransaction result = TransactionRecipientDecoder.Decode(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.ContractCall, result.Kind);
            // The contract itself is treated as the recipient for an opaque call.
            Assert.AreEqual(MANA_CONTRACT, result.Recipient);
            Assert.IsNull(result.TokenContract);
        }

        [Test]
        public void TreatTruncatedTransferCalldataAsContractCall()
        {
            // transfer selector but missing the amount word.
            string data = "0xa9059cbb" + RECIPIENT_WORD;

            DecodedTransaction result = TransactionRecipientDecoder.Decode(MANA_CONTRACT, "0x0", data);

            Assert.AreEqual(TransactionKind.ContractCall, result.Kind);
        }

        [Test]
        public void ParseLargeAmountWordAsPositive()
        {
            // A word with the high bit set must not be interpreted as a negative number.
            string maxWord = new string('f', 64);
            string data = "0xa9059cbb" + RECIPIENT_WORD + maxWord;

            DecodedTransaction result = TransactionRecipientDecoder.Decode(MANA_CONTRACT, "0x0", data);

            Assert.IsTrue(result.Amount > BigInteger.Zero);
        }
    }
}
