using DCL.Web3.Abstract;
using DCL.Web3.Accounts.Factory;
using Nethereum.Signer;
using NUnit.Framework;

namespace DCL.Web3.Tests
{
    [TestFixture]
    public class RustEthereumAccountShould
    {
        // Scalar with a 0x00 top byte: Nethereum's GetPrivateKeyAsBytes() returns the
        // scalar unsigned-trimmed, so this key materializes as 31 bytes (~1/256 of keys).
        private const string LEADING_ZERO_PRIVATE_KEY = "0x0011223344556677889900112233445566778899001122334455667788990011";

        [Test]
        public void CreateAccountWhenPrivateKeyHasLeadingZeroByte()
        {
            var key = new EthECKey(LEADING_ZERO_PRIVATE_KEY);
            Assume.That(key.GetPrivateKeyAsBytes()!.Length, Is.LessThan(32));

            IWeb3Account account = new Web3AccountFactory().CreateAccount(key);

            Assert.That(account.Address.ToString(), Is.EqualTo(key.GetPublicAddress()).IgnoreCase);

            string signature = account.Sign("hello");
            Assert.That(account.Verify("hello", signature), Is.True);
        }

        [Test]
        public void ExposeCanonicalPaddedPrivateKeyHex()
        {
            var key = new EthECKey(LEADING_ZERO_PRIVATE_KEY);

            IWeb3Account account = new Web3AccountFactory().CreateAccount(key);

            Assert.That(account.PrivateKey, Is.EqualTo(LEADING_ZERO_PRIVATE_KEY));
        }
    }
}
