using DCL.AuthenticationScreenFlow;
using DCL.Web3;
using NUnit.Framework;
using System.Numerics;

namespace DCL.EditModeTests
{
    [TestFixture]
    public class TransactionRecipientUtilsShould
    {
        private const string RECIPIENT = "0x430637b3f9c6d36e25f8221b6531390f777e433f";
        private const string MANA_CONTRACT = "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4";

        private static readonly BigInteger FIVE_TOKENS = BigInteger.Parse("5000000000000000000");

        // A name that closes the intentional markup and hides the rest of the warning.
        private const string HOSTILE_NAME = "</b></color></link><size=0>drained";

        [Test]
        public void NeutralizeMarkupInProfileNames()
        {
            string description = TransactionRecipientUtils.ProfileDescription("5 ETH", RECIPIENT, HOSTILE_NAME);

            // The only tags left are the ones this class opened itself.
            Assert.AreEqual(1, CountOccurrences(description, "<link="));
            Assert.AreEqual(1, CountOccurrences(description, "<color="));
            Assert.AreEqual(1, CountOccurrences(description, "<b>"));
            StringAssert.DoesNotContain("<size=0>", description);
            StringAssert.DoesNotContain("</b></color></link><", description);
            // The name is still readable, just inert.
            StringAssert.Contains("drained", description);
        }

        [Test]
        public void NeutralizeMarkupInSceneNames()
        {
            string description = TransactionRecipientUtils.SceneCreatorDescription("5 ETH", "<size=0>hidden");

            StringAssert.DoesNotContain("<size=0>", description);
            StringAssert.Contains("hidden", description);
        }

        [Test]
        public void NeutralizeQuotesThatWouldCloseTheLinkAttribute()
        {
            string description = TransactionRecipientUtils.ProfileDescription("5 ETH", RECIPIENT, "a\"><size=0>b");

            Assert.AreEqual(1, CountOccurrences(description, "<link="));
            StringAssert.DoesNotContain("<size=0>", description);
        }

        [Test]
        public void KeepTheManaSpriteOfTheAmountIntact()
        {
            var decoded = new DecodedTransaction(TransactionKind.Erc20Transfer, RECIPIENT, FIVE_TOKENS, MANA_CONTRACT);

            string amount = TransactionRecipientUtils.Amount(decoded);

            // The amount is built here, not supplied by a scene, so its markup must survive.
            Assert.AreEqual("5 <sprite name=\"MANA\">", amount);
            StringAssert.Contains(amount, TransactionRecipientUtils.ExternalWalletDescription(amount, RECIPIENT));
        }

        [Test]
        public void LeaveOrdinaryNamesUnchanged()
        {
            string description = TransactionRecipientUtils.ProfileDescription("5 ETH", RECIPIENT, "plain_name");

            StringAssert.Contains("@plain_name", description);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;

            for (int i = haystack.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
                count++;

            return count;
        }
    }
}
