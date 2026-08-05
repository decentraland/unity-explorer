using NUnit.Framework;
using System;
using System.Globalization;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     Golden-vector tests: the expected hex constants were generated with the shop web app's
    ///     reference implementation (ethers v5 replica of Server/shop/app/src/lib/trade-encoding.ts and
    ///     buy-gasless.ts; generator preserved in this file's fixture comments). The C# encoder must stay
    ///     byte-identical to the TypeScript encoder — any mismatch is an on-chain failure.
    /// </summary>
    public class CreditsTradeEncoderShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string MAX_CREDITED = "7000000000000000000";
        private const long EXTERNAL_CALL_EXPIRES_AT = 1800000000;

        private const string EXPECTED_ACCEPT_SELECTOR = "0x961a547e";

        private const string EXPECTED_ACCEPT_DATA =
            "0x0000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000" +
            "0000000001000000000000000000000000000000000000000000000000000000000000002000000000000000000000000024e5f44999c151f08609f8" +
            "e27b2238c773c4d02000000000000000000000000000000000000000000000000000000000000000a000000000000000000000000000000000000000" +
            "000000000000000000000001200000000000000000000000000000000000000000000000000000000000000340000000000000000000000000000000" +
            "00000000000000000000000000000004400000000000000000000000000000000000000000000000000000000000000041aaaaaaaaaaaaaaaaaaaaaa" +
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000" +
            "0000000000000000000000000000000000000000000000000070dbd88000000000000000000000000000000000000000000000000000000000677485" +
            "800000000000000000000000000000000000000000000000000000000000001234000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000012000000000000000000000000000000000000000" +
            "000000000000000000000001400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000200000000000000000000000" +
            "001111111111111111111111111111111111111111deadbeef0000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000800000000000000000000000000000000000000000000000000000000000000001000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "010000000000000000000000000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000" +
            "000000000400000000000000000000000022222222222222222222222222222222222222220000000000000000000000000000000000000000000000" +
            "00000000000000000300000000000000000000000099995f38fc9d786eab5c3a1b1c4e6ae5f4e9999900000000000000000000000000000000000000" +
            "000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000200000000000000000000000" +
            "000000000000000000000000000000000000000002000000000000000000000000333333333333333333333333333333333333333300000000000000" +
            "000000000000000000000000000000000022d54fb3923b00000000000000000000000000004444444444444444444444444444444444444444000000" +
            "00000000000000000000000000000000000000000000000000000000a000000000000000000000000000000000000000000000000000000000000000" +
            "00";

        private const string EXPECTED_USE_CREDITS =
            "0x1863572d00000000000000000000000000000000000000000000000000000000000000200000000000000000000000000000000000000000000000" +
            "0000000000000000c0000000000000000000000000000000000000000000000000000000000000014000000000000000000000000000000000000000" +
            "000000000000000000000002000000000000000000000000000000000000000000000000000000000000000860000000000000000000000000000000" +
            "0000000000000000000de0b6b3a76400000000000000000000000000000000000000000000000000006124fee993bc00000000000000000000000000" +
            "00000000000000000000000000000000000000000100000000000000000000000000000000000000000000000053444835ec58000000000000000000" +
            "0000000000000000000000000000000000000000006955b900000000000000000000000000000000000000696e74656e742d6162632d313233000000" +
            "000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000" +
            "200000000000000000000000000000000000000000000000000000000000000041bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" +
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb00000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee961a547e000000000000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000" +
            "000000000000000000000000006b49d200cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd0000000000000000000000" +
            "0000000000000000000000000000000000000005a0000000000000000000000000000000000000000000000000000000000000002000000000000000" +
            "000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000020000000" +
            "00000000000000000024e5f44999c151f08609f8e27b2238c773c4d02000000000000000000000000000000000000000000000000000000000000000" +
            "a00000000000000000000000000000000000000000000000000000000000000120000000000000000000000000000000000000000000000000000000" +
            "000000034000000000000000000000000000000000000000000000000000000000000004400000000000000000000000000000000000000000000000" +
            "000000000000000041aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaa00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "00000000000000000000000000000000010000000000000000000000000000000000000000000000000000000070dbd8800000000000000000000000" +
            "000000000000000000000000000000000067748580000000000000000000000000000000000000000000000000000000000000123400000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001" +
            "200000000000000000000000000000000000000000000000000000000000000140000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000" +
            "0000000000000000200000000000000000000000001111111111111111111111111111111111111111deadbeef000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000" +
            "000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000000000002000000000000000" +
            "000000000000000000000000000000000000000000000000040000000000000000000000002222222222222222222222222222222222222222000000" +
            "000000000000000000000000000000000000000000000000000000000300000000000000000000000099995f38fc9d786eab5c3a1b1c4e6ae5f4e999" +
            "9900000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000" +
            "000000000000000020000000000000000000000000000000000000000000000000000000000000000200000000000000000000000033333333333333" +
            "3333333333333333333333333300000000000000000000000000000000000000000000000022d54fb3923b0000000000000000000000000000444444" +
            "444444444444444444444444444444444400000000000000000000000000000000000000000000000000000000000000a00000000000000000000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";

        private const string EXPECTED_EXECUTE_META_TX =
            "0xd8ed1acc00000000000000000000000099995f38fc9d786eab5c3a1b1c4e6ae5f4e999990000000000000000000000000000000000000000000000" +
            "000000000000000060000000000000000000000000000000000000000000000000000000000000094000000000000000000000000000000000000000" +
            "000000000000000000000008a41863572d00000000000000000000000000000000000000000000000000000000000000200000000000000000000000" +
            "0000000000000000000000000000000000000000c0000000000000000000000000000000000000000000000000000000000000014000000000000000" +
            "000000000000000000000000000000000000000000000002000000000000000000000000000000000000000000000000000000000000000860000000" +
            "0000000000000000000000000000000000000000000de0b6b3a76400000000000000000000000000000000000000000000000000006124fee993bc00" +
            "000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000534448" +
            "35ec580000000000000000000000000000000000000000000000000000000000006955b900000000000000000000000000000000000000696e74656e" +
            "742d6162632d313233000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000" +
            "000000000000000000000000200000000000000000000000000000000000000000000000000000000000000041bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" +
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb00000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee961a547e000000" +
            "0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000a0000000" +
            "000000000000000000000000000000000000000000000000006b49d200cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd" +
            "cd00000000000000000000000000000000000000000000000000000000000005a0000000000000000000000000000000000000000000000000000000" +
            "000000002000000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000" +
            "00000000000000002000000000000000000000000024e5f44999c151f08609f8e27b2238c773c4d02000000000000000000000000000000000000000" +
            "000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000120000000000000000000000000000000" +
            "000000000000000000000000000000034000000000000000000000000000000000000000000000000000000000000004400000000000000000000000" +
            "000000000000000000000000000000000000000041aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa00000000000000000000000000000000000000000000000000000000000000000000" +
            "00000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000070dbd8" +
            "800000000000000000000000000000000000000000000000000000000067748580000000000000000000000000000000000000000000000000000000" +
            "000000123400000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000001200000000000000000000000000000000000000000000000000000000000000140000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000" +
            "0000000000000000000000000000000000000000200000000000000000000000001111111111111111111111111111111111111111deadbeef000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000080000000" +
            "000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000000000000000000000000000" +
            "000000002000000000000000000000000000000000000000000000000000000000000000040000000000000000000000002222222222222222222222" +
            "222222222222222222000000000000000000000000000000000000000000000000000000000000000300000000000000000000000099995f38fc9d78" +
            "6eab5c3a1b1c4e6ae5f4e9999900000000000000000000000000000000000000000000000000000000000000a0000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000000000" +
            "000000000000000000000000000000000000000020000000000000000000000000000000000000000000000000000000000000000200000000000000" +
            "0000000000333333333333333333333333333333333333333300000000000000000000000000000000000000000000000022d54fb3923b0000000000" +
            "000000000000000000444444444444444444444444444444444444444400000000000000000000000000000000000000000000000000000000000000" +
            "a00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
            "0000000041cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" +
            "cccccccccccccccccccc00000000000000000000000000000000000000000000000000000000000000";

        private static TradeDto CreateTradeFixture() =>
            new ()
            {
                id = "trade-1",
                signer = "0x24e5f44999c151f08609f8e27b2238c773c4d020",
                signature = "0x" + new string('a', 130),
                type = "public_item_order",
                network = "matic",
                chainId = 80002,
                contract = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                checks = new TradeChecksDto
                {
                    uses = 1,
                    expiration = 1893456000000, // milliseconds — must normalize to seconds
                    effective = 1735689600000,
                    salt = "0x1234", // short salt — must left-pad to bytes32
                    contractSignatureIndex = 0,
                    signerSignatureIndex = 0,
                    allowedRoot = "0x", // must normalize to the 32-byte zero root
                    externalChecks = new[]
                    {
                        new ExternalCheckDto
                        {
                            contractAddress = "0x1111111111111111111111111111111111111111",
                            selector = "0xdeadbeef",
                            value = null, // must normalize to empty bytes
                            required = true,
                        },
                    },
                },
                sent = new[]
                {
                    new TradeAssetDto
                    {
                        assetType = CreditsTradeEncoder.ASSET_TYPE_COLLECTION_ITEM,
                        contractAddress = "0x2222222222222222222222222222222222222222",
                        itemId = "3",
                    },
                },
                received = new[]
                {
                    new TradeAssetDto
                    {
                        assetType = CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA,
                        contractAddress = "0x3333333333333333333333333333333333333333",
                        amount = "2510000000000000000",
                        beneficiary = "0x4444444444444444444444444444444444444444",
                    },
                },
            };

        private static AuthorizedCredit CreateCreditFixture() =>
            new ()
            {
                id = "intent-abc-123", // non-hex id — must become UTF-8 bytes padded to bytes32
                amount = "6000000000000000000",
                availableAmount = "6000000000000000000",
                expiresAt = 1767225600,
                signature = "0x" + new string('b', 130),
                contract = "0x8052a560e6e6ac86eeb7e711a4497f639b322fb3",
            };

        private static byte[] CreateExternalCallSalt()
        {
            var salt = new byte[32];

            for (var i = 0; i < salt.Length; i++)
                salt[i] = 0xCD;

            return salt;
        }

        [Test]
        public void EncodeAcceptCallMatchingReferenceImplementation()
        {
            // Act
            (byte[] selector, byte[] data) = CreditsTradeEncoder.BuildAcceptCall(CreateTradeFixture(), BUYER);

            // Assert
            Assert.AreEqual(EXPECTED_ACCEPT_SELECTOR, ToHex(selector));
            Assert.AreEqual(EXPECTED_ACCEPT_DATA, ToHex(data));
        }

        [Test]
        public void EncodeUseCreditsCalldataMatchingReferenceImplementation()
        {
            // Act
            string calldata = CreditsTradeEncoder.BuildUseCreditsCalldata(
                CreateTradeFixture(), BUYER, CreateCreditFixture(), MAX_CREDITED, EXTERNAL_CALL_EXPIRES_AT, CreateExternalCallSalt());

            // Assert
            Assert.AreEqual(EXPECTED_USE_CREDITS, calldata);
        }

        [Test]
        public void EncodeExecuteMetaTransactionMatchingReferenceImplementation()
        {
            // Arrange
            string useCredits = CreditsTradeEncoder.BuildUseCreditsCalldata(
                CreateTradeFixture(), BUYER, CreateCreditFixture(), MAX_CREDITED, EXTERNAL_CALL_EXPIRES_AT, CreateExternalCallSalt());

            // Act
            string calldata = CreditsTradeEncoder.BuildExecuteMetaTxCalldata(BUYER, useCredits, "0x" + new string('c', 130));

            // Assert
            StringAssert.StartsWith("0xd8ed1acc", calldata); // deployed CreditsManager selector
            Assert.AreEqual(EXPECTED_EXECUTE_META_TX, calldata);
        }

        /// <summary>
        ///     The relayed meta-transaction reverts with no reason when the head offset of _signature is written
        ///     from the unpadded length of _functionData: the contract then decodes an empty signature and no
        ///     recovery can match the buyer. useCredits calldata is never a multiple of 32 bytes, so this always
        ///     matters — assert the padding rather than trusting the encoder.
        /// </summary>
        [Test]
        public void PadTheFunctionDataBeforeTheSignatureOffset()
        {
            // Arrange
            string useCredits = CreditsTradeEncoder.BuildUseCreditsCalldata(
                CreateTradeFixture(), BUYER, CreateCreditFixture(), MAX_CREDITED, EXTERNAL_CALL_EXPIRES_AT, CreateExternalCallSalt());

            // Act
            string calldata = CreditsTradeEncoder.BuildExecuteMetaTxCalldata(BUYER, useCredits, "0x" + new string('c', 130));

            // Assert
            string args = calldata.Substring("0x".Length + 8);
            int functionDataOffset = HeadWord(args, 1);
            int signatureOffset = HeadWord(args, 2);
            int functionDataLength = WordAt(args, functionDataOffset);
            int paddedLength = (functionDataLength + 31) / 32 * 32;

            Assert.AreEqual((useCredits.Length - "0x".Length) / 2, functionDataLength);
            Assert.AreNotEqual(0, functionDataLength % 32, "The fixture must exercise a functionData length that needs padding");
            Assert.AreEqual(96 + 32 + paddedLength, signatureOffset);
            Assert.AreEqual(65, WordAt(args, signatureOffset), "The contract must decode the whole 65-byte signature");
            Assert.AreEqual(96, functionDataOffset);
        }

        [Test]
        public void CeilUsdWeiToCents()
        {
            Assert.AreEqual(251, CreditsTradeEncoder.UsdWeiToCents("2510000000000000000"));
            Assert.AreEqual(252, CreditsTradeEncoder.UsdWeiToCents("2510000000000000001"));
            Assert.AreEqual(1, CreditsTradeEncoder.UsdWeiToCents("1"));
            Assert.AreEqual(0, CreditsTradeEncoder.UsdWeiToCents(null));
            Assert.AreEqual(0, CreditsTradeEncoder.UsdWeiToCents(string.Empty));
        }

        [Test]
        public void CeilManaWeiToUsdCentsAtTheOracleRate()
        {
            // $0.25 per MANA on an 8-decimal feed: 5 MANA is $1.25.
            var quarterDollar = new ManaUsdRate(25_000_000, 8);
            Assert.AreEqual(125, CreditsTradeEncoder.ManaWeiToUsdCents("5000000000000000000", quarterDollar));

            // The rate conversion floors to USD wei first, so a fraction of a cent has to survive that step to
            // round the price up — 4 MANA wei is the first amount worth a wei more than $1.25 here.
            Assert.AreEqual(125, CreditsTradeEncoder.ManaWeiToUsdCents("5000000000000000001", quarterDollar));
            Assert.AreEqual(126, CreditsTradeEncoder.ManaWeiToUsdCents("5000000000000000004", quarterDollar));

            // The failing purchase: 7 MANA at $0.1348 is 95 cents, not the single credit that was authorized.
            Assert.AreEqual(95, CreditsTradeEncoder.ManaWeiToUsdCents("7000000000000000000", new ManaUsdRate(13_480_500, 8)));

            // Feeds with other precisions are read from the aggregator, never assumed.
            Assert.AreEqual(125, CreditsTradeEncoder.ManaWeiToUsdCents("5000000000000000000", new ManaUsdRate(BigInteger.Parse("250000000000000000"), 18)));

            Assert.AreEqual(0, CreditsTradeEncoder.ManaWeiToUsdCents(null, quarterDollar));
            Assert.AreEqual(0, CreditsTradeEncoder.ManaWeiToUsdCents(string.Empty, quarterDollar));
        }

        [Test]
        public void ConvertUsdWeiToTheManaTheMarketplaceDraws()
        {
            // $2.50 at $0.25 per MANA is the 10 MANA the accept() call transfers.
            var quarterDollar = new ManaUsdRate(25_000_000, 8);
            Assert.AreEqual(BigInteger.Parse("10000000000000000000"), CreditsTradeEncoder.UsdWeiToManaWei("2500000000000000000", quarterDollar));
            Assert.AreEqual(BigInteger.Zero, CreditsTradeEncoder.UsdWeiToManaWei(null, quarterDollar));
        }

        [Test]
        public void RoundCentsUpToWholeCredits()
        {
            Assert.AreEqual(130, CreditsTradeEncoder.RoundUpToWholeCredit(125, 10));
            Assert.AreEqual(130, CreditsTradeEncoder.RoundUpToWholeCredit(130, 10));
            Assert.AreEqual(10, CreditsTradeEncoder.RoundUpToWholeCredit(1, 10));
            Assert.AreEqual(0, CreditsTradeEncoder.RoundUpToWholeCredit(0, 10));
        }

        [Test]
        public void ClampTheUncreditedValueAtZero()
        {
            // The ephemeral credits this flow authorizes are sized exactly to their trade, so nothing is uncredited.
            Assert.AreEqual(BigInteger.Zero, CreditsTradeEncoder.UncreditedValue("7000000000000000000", "7000000000000000000"));
            Assert.AreEqual(BigInteger.Zero, CreditsTradeEncoder.UncreditedValue("6000000000000000000", "7000000000000000000"));
            Assert.AreEqual(BigInteger.Parse("1000000000000000000"), CreditsTradeEncoder.UncreditedValue("7000000000000000000", "6000000000000000000"));
        }

        [Test]
        public void EncodeChainIdAsDomainSalt()
        {
            Assert.AreEqual("0x0000000000000000000000000000000000000000000000000000000000013882", CreditsTradeEncoder.ChainIdSalt(80002));
            Assert.AreEqual("0x0000000000000000000000000000000000000000000000000000000000000089", CreditsTradeEncoder.ChainIdSalt(137));
        }

        [Test]
        public void PadCreditIdsToBytes32Salts()
        {
            // Hex ids are left-padded.
            byte[] hexSalt = CreditsTradeEncoder.IdToSalt("0x1234");
            Assert.AreEqual(32, hexSalt.Length);
            Assert.AreEqual(0x12, hexSalt[30]);
            Assert.AreEqual(0x34, hexSalt[31]);

            // Non-hex ids become UTF-8 bytes, left-padded.
            byte[] utf8Salt = CreditsTradeEncoder.IdToSalt("abc");
            Assert.AreEqual(32, utf8Salt.Length);
            Assert.AreEqual((byte)'a', utf8Salt[29]);
            Assert.AreEqual((byte)'c', utf8Salt[31]);

            // Missing ids become the zero salt.
            Assert.AreEqual(new byte[32], CreditsTradeEncoder.IdToSalt(null));
            Assert.AreEqual(new byte[32], CreditsTradeEncoder.IdToSalt(string.Empty));
        }

        [Test]
        public void BuildMetaTxTypedDataWithCreditsManagerDomain()
        {
            // Arrange
            var chainConfig = new CreditsChainConfig(DCL.Multiplayer.Connections.DecentralandUrls.DecentralandEnvironment.Zone);

            // Act
            string json = CreditsTradeEncoder.BuildMetaTxTypedDataJson(chainConfig, new BigInteger(7), BUYER, "0x1234");
            var typedData = Newtonsoft.Json.Linq.JObject.Parse(json);

            // Assert
            Assert.AreEqual("MetaTransaction", typedData["primaryType"]!.ToString());
            Assert.AreEqual("Decentraland Credits", typedData["domain"]!["name"]!.ToString());
            Assert.AreEqual("1.0.0", typedData["domain"]!["version"]!.ToString());
            Assert.AreEqual("0x8052a560e6e6ac86eeb7e711a4497f639b322fb3", typedData["domain"]!["verifyingContract"]!.ToString());
            Assert.AreEqual("0x0000000000000000000000000000000000000000000000000000000000013882", typedData["domain"]!["salt"]!.ToString());
            Assert.AreEqual("7", typedData["message"]!["nonce"]!.ToString());
            Assert.AreEqual(BUYER, typedData["message"]!["from"]!.ToString());
            Assert.AreEqual("0x1234", typedData["message"]!["functionData"]!.ToString());
            Assert.AreEqual(3, ((Newtonsoft.Json.Linq.JArray)typedData["types"]!["MetaTransaction"]!).Count);
            Assert.AreEqual(4, ((Newtonsoft.Json.Linq.JArray)typedData["types"]!["EIP712Domain"]!).Count);
        }

        /// <summary>
        ///     The value of the ABI head word at the given parameter index, as a byte count.
        /// </summary>
        private static int HeadWord(string argsHex, int index) =>
            WordAt(argsHex, index * 32);

        /// <summary>
        ///     The 32-byte ABI word starting at the given byte offset into the argument block.
        /// </summary>
        private static int WordAt(string argsHex, int byteOffset) =>
            (int)BigInteger.Parse($"0{argsHex.Substring(byteOffset * 2, 64)}", NumberStyles.HexNumber);

        private static string ToHex(byte[] bytes) =>
            "0x" + BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
