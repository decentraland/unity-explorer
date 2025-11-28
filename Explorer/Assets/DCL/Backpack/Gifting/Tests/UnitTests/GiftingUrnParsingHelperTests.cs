using DCL.Backpack.Gifting.Utils;
using NUnit.Framework;

namespace DCL.Backpack.Gifting.Tests
{
    [TestFixture]
    public class GiftingUrnParsingHelperShould
    {
        [Test]
        public void ReturnBaseUrnWhenUrnIsStandardDecentralandFormat()
        {
            string input = "urn:decentraland:matic:collections-v2:0x32b7495895264ac9d0b12d32afd435453458b1c6:123";
            string expected = "urn:decentraland:matic:collections-v2:0x32b7495895264ac9d0b12d32afd435453458b1c6";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsTrue(result);
            Assert.AreEqual(expected, baseUrn);
        }

        [Test]
        public void ReturnBaseUrnWhenUrnIsShort()
        {
            string input = "urn:1";
            string expected = "urn";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsTrue(result);
            Assert.AreEqual(expected, baseUrn);
        }

        [Test]
        public void ReturnBaseUrnWhenUrnHasMultipleSegments()
        {
            string input = "segment1:segment2:segment3:tokenId";
            string expected = "segment1:segment2:segment3";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsTrue(result);
            Assert.AreEqual(expected, baseUrn);
        }

        [Test]
        public void ReturnFalseWhenStringIsEmpty()
        {
            string input = "";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
            Assert.IsEmpty(baseUrn);
        }

        [Test]
        public void ReturnFalseWhenStringIsNull()
        {
            string? input = null;

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
            Assert.IsEmpty(baseUrn);
        }

        [Test]
        public void ReturnFalseWhenUrnHasNoColon()
        {
            string input = "urn_without_separator";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
        }

        [Test]
        public void ReturnFalseWhenColonIsAtStart()
        {
            string input = ":123";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
        }

        [Test]
        public void ReturnFalseWhenColonIsAtEnd()
        {
            string input = "urn:decentraland:";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
        }

        [Test]
        public void ReturnFalseWhenStringIsJustColon()
        {
            string input = ":";

            bool result = GiftingUrnParsingHelper.TryGetBaseUrn(input, out string baseUrn);

            Assert.IsFalse(result);
        }
    }
}