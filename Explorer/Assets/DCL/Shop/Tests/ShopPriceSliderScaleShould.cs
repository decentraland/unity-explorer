using NUnit.Framework;

namespace DCL.Shop.Tests
{
    public class ShopPriceSliderScaleShould
    {
        [TestCase(0, 0)]
        [TestCase(1, 10)]
        [TestCase(90, 900)]
        [TestCase(91, 2000)]
        [TestCase(180, 100_000)]
        [TestCase(500, 100_000)]
        [TestCase(-3, 0)]
        public void MapStepsToCredits(int step, int expectedCredits)
        {
            // Act
            int credits = ShopPriceSliderScale.StepToCredits(step);

            // Assert
            Assert.AreEqual(expectedCredits, credits);
        }

        [Test]
        public void GiveEveryStepItsOwnPriceAndMapItBack()
        {
            // Arrange
            int previous = -1;

            for (var step = 0; step <= ShopPriceSliderScale.STEPS; step++)
            {
                // Act
                int credits = ShopPriceSliderScale.StepToCredits(step);

                // Assert
                Assert.Greater(credits, previous, $"step {step}");
                Assert.AreEqual(step, ShopPriceSliderScale.CreditsToStep(credits), $"step {step}");
                previous = credits;
            }
        }

        [TestCase(250_000, ShopPriceSliderScale.STEPS)]
        [TestCase(-5, 0)]
        [TestCase(14, 1)]
        public void ClampAndRoundPricesOntoTheTrack(int credits, int expectedStep)
        {
            // Act
            int step = ShopPriceSliderScale.CreditsToStep(credits);

            // Assert
            Assert.AreEqual(expectedStep, step);
        }
    }
}
