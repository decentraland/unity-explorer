using NUnit.Framework;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The money split: which methods a pair of balances supports and how much MANA covers what credits do
    ///     not. Mirrors the cases of the shop web app's payment-options.spec.ts.
    /// </summary>
    public class PaymentOptionsCalculatorShould
    {
        private const int PRICE_CENTS = 250;

        // 10 MANA: the price at $0.25 per MANA.
        private static readonly BigInteger PRICE_MANA_WEI = BigInteger.Parse("10000000000000000000");
        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");

        [Test]
        public void OfferOnlyCreditsWhenTheBuyerHoldsNoMana()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 300, BigInteger.Zero);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.AreEqual(PRICE_CENTS, options.Options[0].CreditsCents);
            Assert.AreEqual(25, options.Options[0].Credits);
            Assert.IsNull(options.ManaShortfall);
        }

        [Test]
        public void OfferCreditsFirstThenManaWhenBothCoverThePrice()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 250, PRICE_MANA_WEI);

            // Assert: a full credit balance leaves no remainder, so there is no mixed option.
            Assert.AreEqual(2, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Credits, options.Options[0].Method);
            Assert.AreEqual(PaymentMethod.Mana, options.Options[1].Method);
            Assert.AreEqual(PRICE_MANA_WEI, options.Options[1].ManaWei);
        }

        [Test]
        public void FloorTheCreditsLegToWholeCreditsAndCoverTheRestInMana()
        {
            // Act: 107 cents is 10 whole credits, so 150 of the 250 cents fall on MANA: 6 MANA.
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 107, 6 * ONE_MANA);

            // Assert
            Assert.IsTrue(options.TryGet(PaymentMethod.CreditsAndMana, out PaymentOption mixed));
            Assert.AreEqual(100, mixed.CreditsCents);
            Assert.AreEqual(10, mixed.Credits);
            Assert.AreEqual(6 * ONE_MANA, mixed.ManaWei);
            Assert.IsFalse(options.TryGet(PaymentMethod.Credits, out _));
            Assert.IsFalse(options.TryGet(PaymentMethod.Mana, out _));
        }

        [Test]
        public void ReportTheManaThatCannotPayInsteadOfOfferingIt()
        {
            // Act: 2 MANA is worth 50 cents here and the buyer has no credits to mix it with.
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 0, 2 * ONE_MANA);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.IsNotNull(options.ManaShortfall);
            Assert.AreEqual(50, options.ManaShortfall!.Value.ManaCents);
            Assert.AreEqual(PRICE_CENTS, options.ManaShortfall!.Value.PriceCents);
        }

        [Test]
        public void RecordThePriceAndTheBalancesItWasGiven()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 300, 4 * ONE_MANA);

            // Assert
            Assert.AreEqual(PRICE_CENTS, options.PriceCents);
            Assert.AreEqual(PRICE_MANA_WEI, options.PriceManaWei);
            Assert.AreEqual(300, options.CreditsBalanceCents);
            Assert.AreEqual(4 * ONE_MANA, options.ManaBalanceWei);
        }

        [Test]
        public void TreatAnUnknownBalanceAsNothingHeldAndKeepItUnknown()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, null, null);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.IsNull(options.CreditsBalanceCents);
            Assert.IsNull(options.ManaBalanceWei);
        }

        [Test]
        public void KeepTheBalancesOfAPriceThatIsNothing()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(0, BigInteger.Zero, 300, ONE_MANA);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.AreEqual(300, options.CreditsBalanceCents);
            Assert.AreEqual(ONE_MANA, options.ManaBalanceWei);
        }

        [Test]
        public void LeaveManaOutAsAMethodOfItsOwnWhenTheRelayerWouldRefuseIt()
        {
            // Act: plenty of MANA both times, with 10 credits and then with none.
            PaymentOptions withCredits = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 100, 20 * ONE_MANA, false);
            PaymentOptions withoutCredits = PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 0, 20 * ONE_MANA, false);

            // Assert: MANA still tops up credits, and holding enough of it is not reported as a shortfall.
            Assert.AreEqual(1, withCredits.Options.Count);
            Assert.AreEqual(PaymentMethod.CreditsAndMana, withCredits.Options[0].Method);
            Assert.AreEqual(0, withoutCredits.Options.Count);
            Assert.IsNull(withoutCredits.ManaShortfall);
            Assert.IsTrue(withoutCredits.IsManaAloneRefused);
        }

        [Test]
        public void OfferNoManaMethodWhenTheManaPriceIsUnknown()
        {
            // Act
            PaymentOptions options = PaymentOptionsCalculator.Compute(PRICE_CENTS, BigInteger.Zero, 100, 100 * ONE_MANA);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.IsNull(options.ManaShortfall);
        }

        [Test]
        public void RoundTheManaRemainderUp()
        {
            // Act: a third of 10 wei is 3.33 wei.
            BigInteger gap = PaymentOptionsCalculator.ManaForRemainder(1, 3, new BigInteger(10));

            // Assert
            Assert.AreEqual(new BigInteger(4), gap);
        }

        [Test]
        public void SpreadWholeCreditsOverTheUnitsInOrder()
        {
            // Act: 307 cents is 30 whole credits; the second unit takes what is left and the third takes none.
            int[] allocation = PaymentOptionsCalculator.DistributeCredits(new[] { 250, 130, 130 }, 307);

            // Assert
            CollectionAssert.AreEqual(new[] { 250, 50, 0 }, allocation);
        }

        [Test]
        public void SizeAMixedPaymentFromItsCredit()
        {
            // Act: 100 of 250 cents in credits on a 10 MANA price; the credit is worth 4 MANA.
            bool sized = PaymentOptionsCalculator.TrySizeMixedPayment((4 * ONE_MANA).ToString(), 100, 250, 10 * ONE_MANA, out BigInteger gapWei, out string maxCreditedValue);

            // Assert: the remainder's share, and the cap that lets it be pulled on top of the credit.
            Assert.IsTrue(sized);
            Assert.AreEqual(6 * ONE_MANA, gapWei);
            Assert.AreEqual((10 * ONE_MANA).ToString(), maxCreditedValue);
        }

        [Test]
        public void RaiseTheGapWhenTheCreditIsWorthLessThanItsShare()
        {
            // Act: the credit is worth 3 MANA where its share of the price is 4.
            PaymentOptionsCalculator.TrySizeMixedPayment((3 * ONE_MANA).ToString(), 100, 250, 10 * ONE_MANA, out BigInteger gapWei, out string maxCreditedValue);

            // Assert
            Assert.AreEqual(7 * ONE_MANA, gapWei);
            Assert.AreEqual((10 * ONE_MANA).ToString(), maxCreditedValue);
        }

        [Test]
        public void RefuseToSizeAMixedPaymentFromAnUnreadableCredit()
        {
            // Assert
            Assert.IsFalse(PaymentOptionsCalculator.TrySizeMixedPayment("not-a-number", 100, 250, 10 * ONE_MANA, out _, out _));
        }

        [Test]
        public void TolerateASmallManaDriftOnly()
        {
            // Assert
            Assert.IsFalse(PaymentOptionsCalculator.ExceedsConfirmedMana(102 * ONE_MANA, 100 * ONE_MANA));
            Assert.IsTrue(PaymentOptionsCalculator.ExceedsConfirmedMana(103 * ONE_MANA, 100 * ONE_MANA));
        }
    }
}
