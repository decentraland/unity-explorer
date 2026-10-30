using NUnit.Framework;
using UnityEngine.TestTools;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class MarketplaceCreditsAPIClientCheckoutShould
    {
        [TestCase(404L, CreditsCheckoutError.FeatureDisabled)]
        [TestCase(503L, CreditsCheckoutError.PaymentsUnavailable)]
        [TestCase(400L, CreditsCheckoutError.UnknownPack)]
        [TestCase(502L, CreditsCheckoutError.ProviderError)]
        [TestCase(500L, CreditsCheckoutError.NetworkError)]
        [TestCase(401L, CreditsCheckoutError.NetworkError)]
        public void MapCheckoutStatusCodesToErrors(long responseCode, CreditsCheckoutError expected)
        {
            // Act
            CreditsCheckoutError error = MarketplaceCreditsAPIClient.MapCheckoutStatusCode(responseCode);

            // Assert
            Assert.AreEqual(expected, error);
        }

        [Test]
        public void ExtractServerErrorMessageFromBody()
        {
            // Act
            string message = MarketplaceCreditsAPIClient.ParseErrorMessage("{\"error\":\"Unknown pack\"}", "test");

            // Assert
            Assert.AreEqual("Unknown pack", message);
        }

        [Test]
        public void FallBackToEmptyMessageWhenBodyIsEmpty()
        {
            // Act
            string message = MarketplaceCreditsAPIClient.ParseErrorMessage(string.Empty, "test");

            // Assert
            Assert.AreEqual(string.Empty, message);
        }

        [Test]
        public void FallBackToEmptyMessageWhenBodyIsNotJson()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;

            try
            {
                // Act
                string message = MarketplaceCreditsAPIClient.ParseErrorMessage("<html>Bad Gateway</html>", "test");

                // Assert
                Assert.AreEqual(string.Empty, message);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
