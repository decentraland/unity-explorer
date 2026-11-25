using NUnit.Framework;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The two tick boxes over the option sets the calculator can return. Prices are whole credits, so
    ///     credits alone and credits + MANA never come together.
    /// </summary>
    public class PaymentSelectionShould
    {
        private const int PRICE_CENTS = 250;

        // 10 MANA: the price at $0.25 per MANA.
        private static readonly BigInteger PRICE_MANA_WEI = BigInteger.Parse("10000000000000000000");
        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");

        [Test]
        public void StartOnCreditsWhenTheyCoverThePrice()
        {
            // Arrange
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 300, 20 * ONE_MANA));

            // Assert
            Assert.IsTrue(selection.CreditsTicked);
            Assert.IsFalse(selection.ManaTicked);
            Assert.IsTrue(selection.TryGetSelected(out PaymentOption selected));
            Assert.AreEqual(PaymentMethod.Credits, selected.Method);
            Assert.AreEqual(25, selection.CreditsAmount);
            Assert.AreEqual(PRICE_MANA_WEI, selection.ManaAmountWei);
        }

        [Test]
        public void StartWithBothTickedWhenOnlyTheirSumCoversThePrice()
        {
            // Arrange: 10 credits pay $1.00, the other $1.50 is 6 MANA.
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 100, 6 * ONE_MANA));

            // Assert
            Assert.IsTrue(selection.CreditsTicked);
            Assert.IsTrue(selection.ManaTicked);
            Assert.IsTrue(selection.TryGetSelected(out PaymentOption selected));
            Assert.AreEqual(PaymentMethod.CreditsAndMana, selected.Method);
            Assert.AreEqual(10, selection.CreditsAmount);
            Assert.AreEqual(6 * ONE_MANA, selection.ManaAmountWei);
        }

        [Test]
        public void StartOnManaWhenThereAreNoCreditsToSpend()
        {
            // Arrange
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 0, 20 * ONE_MANA));

            // Assert
            Assert.IsFalse(selection.CreditsTicked);
            Assert.IsTrue(selection.ManaTicked);
            Assert.IsFalse(selection.CreditsAvailable);
            Assert.IsTrue(selection.ManaAvailable);
        }

        [Test]
        public void SelectNothingAndOfferNoRowWhenNothingIsPayable()
        {
            // Arrange
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 50, ONE_MANA));

            // Act
            bool creditsChanged = selection.ToggleCredits();
            bool manaChanged = selection.ToggleMana();

            // Assert
            Assert.IsFalse(creditsChanged);
            Assert.IsFalse(manaChanged);
            Assert.IsFalse(selection.HasSelection);
            Assert.IsFalse(selection.CreditsAvailable);
            Assert.IsFalse(selection.ManaAvailable);
            Assert.IsFalse(selection.TryGetSelected(out _));
        }

        [Test]
        public void SwitchBetweenCreditsAndManaWhenEachCoversThePrice()
        {
            // Arrange
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 300, 20 * ONE_MANA));

            // Act
            bool toMana = selection.ToggleMana();
            bool manaTickedAlone = selection.ManaTicked && !selection.CreditsTicked;
            bool toCredits = selection.ToggleCredits();

            // Assert
            Assert.IsTrue(toMana);
            Assert.IsTrue(manaTickedAlone);
            Assert.IsTrue(toCredits);
            Assert.IsTrue(selection.CreditsTicked);
            Assert.IsFalse(selection.ManaTicked);
        }

        [Test]
        public void KeepTheOnlyTickedRowTicked()
        {
            // Arrange: credits cover the price and there is no MANA.
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 300, BigInteger.Zero));

            // Act
            bool unticked = selection.ToggleCredits();
            bool manaTicked = selection.ToggleMana();

            // Assert
            Assert.IsFalse(unticked);
            Assert.IsFalse(manaTicked);
            Assert.IsTrue(selection.CreditsTicked);
            Assert.IsFalse(selection.ManaAvailable);
        }

        [Test]
        public void KeepBothTickedWhenNeitherCoversThePriceAlone()
        {
            // Arrange
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 100, 6 * ONE_MANA));

            // Act
            bool creditsChanged = selection.ToggleCredits();
            bool manaChanged = selection.ToggleMana();

            // Assert
            Assert.IsFalse(creditsChanged);
            Assert.IsFalse(manaChanged);
            Assert.IsTrue(selection.CreditsTicked);
            Assert.IsTrue(selection.ManaTicked);
        }

        [Test]
        public void DropAndRestoreTheCreditsLegWhenManaAloneAlsoCoversThePrice()
        {
            // Arrange: credits are short, MANA covers the price with or without them.
            var selection = new PaymentSelection(PaymentOptionsCalculator.Compute(PRICE_CENTS, PRICE_MANA_WEI, 100, 20 * ONE_MANA));

            // Act
            bool startsMixed = selection.CreditsTicked && selection.ManaTicked;
            bool manaUnticked = selection.ToggleMana();
            bool creditsUnticked = selection.ToggleCredits();
            bool paysManaAlone = selection.TryGetSelected(out PaymentOption manaOnly) && manaOnly.Method == PaymentMethod.Mana;
            BigInteger fullManaPrice = selection.ManaAmountWei;
            bool creditsTickedAgain = selection.ToggleCredits();

            // Assert
            Assert.IsTrue(startsMixed);
            Assert.IsFalse(manaUnticked);
            Assert.IsTrue(creditsUnticked);
            Assert.IsTrue(paysManaAlone);
            Assert.AreEqual(PRICE_MANA_WEI, fullManaPrice);
            Assert.IsTrue(creditsTickedAgain);
            Assert.IsTrue(selection.TryGetSelected(out PaymentOption mixed));
            Assert.AreEqual(PaymentMethod.CreditsAndMana, mixed.Method);
            Assert.AreEqual(6 * ONE_MANA, selection.ManaAmountWei);
        }
    }
}
