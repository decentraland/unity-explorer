using DCL.Web3.Accounts;
using Nethereum.Signer;
using NUnit.Framework;

namespace Plugins.RustEthereum.SignServerWrap.Tests
{
    public class ValidSigningTest
    {
        private const string TEST_KEY = "0x64fdd126fe0e2de2ccbea065d710e9939d083ec96bb9933b750013f30ee81004";
        private const string MESSAGE = "Test message";

        [Test]
        public void TestSign()
        {
            var key = new EthECKey(TEST_KEY);
            var verifiedAccount = NethereumAccount.CreateForVerifyOnly(key);
            string verifiedSignature = verifiedAccount.Sign(MESSAGE + MESSAGE + MESSAGE + MESSAGE);

            var testableAccount = new RustEthereumAccount(key);
            string signature = testableAccount.Sign(MESSAGE);

            Assert.True(
                verifiedAccount.Verify(MESSAGE, signature),
                $"Failed to verify the signature: {signature}\nit should be: {verifiedSignature}"
            );
        }

        [Test]
        public void TestVerify()
        {
            var account = new RustEthereumAccount(new EthECKey(TEST_KEY));
            string signature= account.Sign(MESSAGE);
            Assert.True(account.Verify(MESSAGE, signature));
        }
    }
}
