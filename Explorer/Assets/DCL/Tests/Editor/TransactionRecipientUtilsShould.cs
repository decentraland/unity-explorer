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

            // Spelled the way the PolygonManaIcon character table spells it: TMP matches sprite names
            // case-sensitively, and a mismatch renders as tofu rather than failing loudly.
            Assert.AreEqual("5 <sprite name=\"Mana\">", amount);
            // The amount is built here, not supplied by a scene, so its markup must survive.
            StringAssert.Contains(amount, TransactionRecipientUtils.ExternalWalletDescription(amount, RECIPIENT));
        }

        [Test]
        public void SayAnAmountIsSmallRatherThanCallItZero()
        {
            // A single wei: below the four fraction digits the copy shows.
            var decoded = new DecodedTransaction(TransactionKind.NativeTransfer, RECIPIENT, BigInteger.One, null);

            string amount = TransactionRecipientUtils.Amount(decoded);

            Assert.AreEqual("under 0.0001 ETH", amount);
        }

        [Test]
        public void CallAGenuineZeroZero()
        {
            var decoded = new DecodedTransaction(TransactionKind.NativeTransfer, RECIPIENT, BigInteger.Zero, null);

            Assert.AreEqual("0 ETH", TransactionRecipientUtils.Amount(decoded));
        }

        [Test]
        public void KeepMarkupOutOfTheDustAmount()
        {
            // The amount is interpolated into rich text unescaped, so it must never introduce a tag.
            var decoded = new DecodedTransaction(TransactionKind.NativeTransfer, RECIPIENT, BigInteger.One, null);

            StringAssert.DoesNotContain("<", TransactionRecipientUtils.Amount(decoded));
        }

        [Test]
        public void KeepFourFractionDigits()
        {
            // 0.0001 exactly: the smallest amount that still renders as a number.
            var decoded = new DecodedTransaction(TransactionKind.NativeTransfer, RECIPIENT, BigInteger.Pow(10, 14), null);

            Assert.AreEqual("0.0001 ETH", TransactionRecipientUtils.Amount(decoded));
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
