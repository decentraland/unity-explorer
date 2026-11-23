using NUnit.Framework;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class PaymentAmountFormatShould
    {
        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");
        private static readonly BigInteger ONE_THOUSANDTH = BigInteger.Parse("1000000000000000");

        [TestCase(0, ExpectedResult = "0")]
        [TestCase(700, ExpectedResult = "700")]
        [TestCase(7000, ExpectedResult = "7,000")]
        [TestCase(1234567, ExpectedResult = "1,234,567")]
        public string GroupCreditsByThousands(long credits) =>
            PaymentAmountFormat.Credits(credits);

        [Test]
        public void GroupWholeManaByThousandsAndDropEmptyDecimals()
        {
            // Act
            string price = PaymentAmountFormat.ManaPrice(3400 * ONE_MANA);
            string balance = PaymentAmountFormat.ManaBalance(3400 * ONE_MANA);

            // Assert
            Assert.AreEqual("3,400", price);
            Assert.AreEqual("3,400", balance);
        }

        [Test]
        public void RoundAPriceUpAndABalanceDown()
        {
            // Arrange: 13.514 MANA.
            BigInteger amount = (13 * ONE_MANA) + (514 * ONE_THOUSANDTH);

            // Act
            string price = PaymentAmountFormat.ManaPrice(amount);
            string balance = PaymentAmountFormat.ManaBalance(amount);

            // Assert
            Assert.AreEqual("13.52", price);
            Assert.AreEqual("13.51", balance);
        }

        [Test]
        public void TrimATrailingZeroDecimal()
        {
            // Act
            string balance = PaymentAmountFormat.ManaBalance((2 * ONE_MANA) + (500 * ONE_THOUSANDTH));

            // Assert
            Assert.AreEqual("2.5", balance);
        }

        [Test]
        public void NeverShowAPositivePriceAsZero()
        {
            // Act
            string price = PaymentAmountFormat.ManaPrice(BigInteger.One);
            string balance = PaymentAmountFormat.ManaBalance(BigInteger.One);

            // Assert
            Assert.AreEqual("0.01", price);
            Assert.AreEqual("0", balance);
        }

        [Test]
        public void GiveTheManaOneCreditIsWorth()
        {
            // Act: 50 credits for 500 MANA, then 25 credits for 9.25 MANA.
            string round = PaymentAmountFormat.Rate(500, 500 * ONE_MANA);
            string fractional = PaymentAmountFormat.Rate(250, (9 * ONE_MANA) + (250 * ONE_THOUSANDTH));

            // Assert
            Assert.AreEqual("1 Credit = 10 MANA", round);
            Assert.AreEqual("1 Credit = 0.37 MANA", fractional);
        }

        [Test]
        public void GiveASmallRateMoreDecimalsInsteadOfZero()
        {
            // Act: 100 credits for half a MANA.
            string rate = PaymentAmountFormat.Rate(1000, 500 * ONE_THOUSANDTH);

            // Assert
            Assert.AreEqual("1 Credit = 0.005 MANA", rate);
        }

        [Test]
        public void GiveAnUnknownRateWhenThePriceHasNoManaEquivalent()
        {
            // Act
            string rate = PaymentAmountFormat.Rate(250, BigInteger.Zero);

            // Assert
            Assert.AreEqual("1 Credit = - MANA", rate);
        }
    }
}
