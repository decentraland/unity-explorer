using NUnit.Framework;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class Web3AddressShould
    {
        private const string VALID_ADDRESS = "0x1111111111111111111111111111111111111111";

        [TestCase("0xabcdefabcdefabcdefabcdefabcdefabcdefabcd", TestName = "lowercase hex")]
        [TestCase("0xABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD", TestName = "uppercase hex")]
        [TestCase("0x71C7656EC7ab88b098defB751B7401B5f6d8976F", TestName = "checksummed mixed case")]
        [TestCase("0X1111111111111111111111111111111111111111", TestName = "uppercase X prefix")]
        [TestCase("0x0000000000000000000000000000000000000000", TestName = "zero address")]
        public void AcceptWellFormedWalletAddress(string candidate)
        {
            Assert.That(Web3Address.IsValidWalletAddress(candidate), Is.True);
        }

        [TestCase(null, TestName = "null")]
        [TestCase("", TestName = "empty")]
        [TestCase("0x111111111111111111111111111111111111111", TestName = "one hex digit short")]
        [TestCase("0x11111111111111111111111111111111111111111", TestName = "one hex digit long")]
        [TestCase("1111111111111111111111111111111111111111111", TestName = "no 0x prefix")]
        [TestCase("111111111111111111111111111111111111111111", TestName = "42 chars but no prefix")]
        [TestCase("0x111111111111111111111111111111111111111g", TestName = "non hex digit")]
        [TestCase("0x1111111111111111111111111111111111111 11", TestName = "embedded space")]
        [TestCase("not-a-wallet-address", TestName = "arbitrary text")]
        public void RejectMalformedWalletAddress(string? candidate)
        {
            Assert.That(Web3Address.IsValidWalletAddress(candidate), Is.False);
        }

        [Test]
        public void ValidateAgainstItsOwnLengthConstant()
        {
            // Arrange / Act / Assert — the validator and the constant must not drift apart.
            Assert.That(VALID_ADDRESS.Length, Is.EqualTo(Web3Address.ETH_ADDRESS_LENGTH));
            Assert.That(Web3Address.IsValidWalletAddress(VALID_ADDRESS), Is.True);
        }
    }
}
