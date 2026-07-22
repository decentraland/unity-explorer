using DCL.Web3;
using NUnit.Framework;

namespace DCL.AuthenticationScreenFlow.Tests
{
    [TestFixture]
    public class ReferrerArgShould
    {
        private const string VALID_REFERRER = "0x24e5f44999c151f08609f8e27b2238c773c4d020";

        [Test]
        public void KeepValidLowercaseAddress()
        {
            Assert.AreEqual(VALID_REFERRER, ReferrerArg.Normalize(VALID_REFERRER));
        }

        [Test]
        public void LowercaseMixedCaseAddress()
        {
            Assert.AreEqual(VALID_REFERRER, ReferrerArg.Normalize("0x24E5F44999C151F08609F8E27B2238C773C4D020"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0x123")]
        [TestCase("not-an-address")]
        [TestCase("javascript:alert(1)")]
        [TestCase("0xZZZ5f44999c151f08609f8e27b2238c773c4d020")]
        [TestCase(" 0x24e5f44999c151f08609f8e27b2238c773c4d020")]
        [TestCase("0x24e5f44999c151f08609f8e27b2238c773c4d0201")]
        public void RejectInvalidValues(string? referrer)
        {
            Assert.IsNull(ReferrerArg.Normalize(referrer));
        }
    }
}
