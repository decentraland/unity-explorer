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
        public void TolerateASmallManaDriftOnly()
        {
            // Assert
            Assert.IsFalse(PaymentOptionsCalculator.ExceedsConfirmedMana(102 * ONE_MANA, 100 * ONE_MANA));
            Assert.IsTrue(PaymentOptionsCalculator.ExceedsConfirmedMana(103 * ONE_MANA, 100 * ONE_MANA));
        }
    }
}
