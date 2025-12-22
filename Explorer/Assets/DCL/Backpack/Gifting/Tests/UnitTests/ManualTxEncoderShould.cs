using System;
using DCL.Backpack.Gifting.Utils;
using NUnit.Framework;

namespace DCL.Backpack.Gifting.Tests
{
    [TestFixture]
    public class ManualTxEncoderShould
    {
        // 0x + 23b872dd (selector)
        private const string SELECTOR = "0x23b872dd";

        [Test]
        public void EncodeTransferFromCorrectly()
        {
            // Real-looking addresses
            string from = "0x8a192c7326227b6534d402482386926362d29443";
            string to = "0x430637b3f9c6d36e25f8221b6531390f777e433f";
            string tokenId = "123456";

            // Expected Result Calculation:
            // 1. Selector: 23b872dd
            // 2. From: 0000000000000000000000008a192c7326227b6534d402482386926362d29443 (Padded to 64 chars)
            // 3. To:   000000000000000000000000430637b3f9c6d36e25f8221b6531390f777e433f (Padded to 64 chars)
            // 4. ID:   123456 decimal = 1E240 hex. Padded: 000000000000000000000000000000000000000000000000000000000001e240

            string expectedData = "0x23b872dd" +
                                  "0000000000000000000000008a192c7326227b6534d402482386926362d29443" +
                                  "000000000000000000000000430637b3f9c6d36e25f8221b6531390f777e433f" +
                                  "000000000000000000000000000000000000000000000000000000000001e240";

            string result = ManualTxEncoder.EncodeTransferFrom(from, to, tokenId);
            Assert.AreEqual(expectedData.ToLowerInvariant(), result.ToLowerInvariant());
        }

        [Test]
        public void ThrowExceptionWhenAddressIsInvalidLength()
        {
            string shortAddr = "0x123";
            Assert.Throws<ArgumentException>(() =>
                ManualTxEncoder.EncodeTransferFrom(shortAddr, shortAddr, "1"));
        }

        [Test]
        public void HandleAddressWithoutPrefix()
        {
            string from = "8a192c7326227b6534d402482386926362d29443";
            string to = "430637b3f9c6d36e25f8221b6531390f777e433f";
            string tokenId = "1";

            string result = ManualTxEncoder.EncodeTransferFrom(from, to, tokenId);

            Assert.IsTrue(result.StartsWith("0x23b872dd"));

            // Total length: 2 (0x) + 8 (selector) + 64 (from) + 64 (to) + 64 (id) = 202 chars
            Assert.AreEqual(202, result.Length);
        }
    }
}