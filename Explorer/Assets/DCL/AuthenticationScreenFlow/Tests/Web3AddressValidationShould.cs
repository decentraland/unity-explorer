using DCL.Web3;
using NUnit.Framework;

namespace DCL.AuthenticationScreenFlow.Tests
{
    /// <summary>
    ///     Covers <see cref="Web3Address.FromUntrusted" /> only — the wrapping/normalizing helper.
    ///     The underlying <see cref="Web3Address.IsValidWalletAddress" /> validation is covered by
    ///     <c>DCL.Web3.Tests.Web3AddressShould</c>.
    /// </summary>
    [TestFixture]
    public class Web3AddressValidationShould
    {
        private const string VALID_REFERRER = "0x24e5f44999c151f08609f8e27b2238c773c4d020";

        [Test]
        public void WrapValidAddress()
        {
            Assert.AreEqual(VALID_REFERRER, Web3Address.FromUntrusted(VALID_REFERRER)!.Value.ToString());
        }

        [Test]
        public void LowercaseMixedCaseAddress()
        {
            Assert.AreEqual(VALID_REFERRER, Web3Address.FromUntrusted("0x24E5F44999C151F08609F8E27B2238C773C4D020")!.Value.ToString());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0x123")]
        [TestCase("not-an-address")]
        [TestCase("javascript:alert(1)")]
        [TestCase("0xZZZ5f44999c151f08609f8e27b2238c773c4d020")]
        [TestCase(" 0x24e5f44999c151f08609f8e27b2238c773c4d020")]
        [TestCase("0x24e5f44999c151f08609f8e27b2238c773c4d0201")]
        public void DegradeInvalidValuesToNull(string? value)
        {
            Assert.IsNull(Web3Address.FromUntrusted(value));
        }
    }
}
