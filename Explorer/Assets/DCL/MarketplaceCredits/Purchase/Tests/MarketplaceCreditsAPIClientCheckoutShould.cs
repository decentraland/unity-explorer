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

        /// <summary>
        ///     THE AUTHORIZE BODY CARRIES ONLY THE KEYS THAT APPLY.
        ///
        ///     The server validates `contractAddress`/`itemId` as a pair the moment either key is PRESENT, and an
        ///     empty string counts as present. Sending both as `""` on a trade purchase therefore fails its address
        ///     check with a 400 — which is exactly what happened: a mint went through (its pair was real) while
        ///     buying a trade-backed item came back
        ///     `"contractAddress" must be a valid address when an item is given`.
        ///
        ///     `JsonUtility` cannot omit a field, so the shape is chosen per rail. These tests pin that no request
        ///     ever carries an empty identifier.
        /// </summary>
        [Test]
        public void SendOnlyTheTradeIdForATradePurchase()
        {
            // Act
            string body = MarketplaceCreditsAPIClient.BuildAuthorizeBody(110, "558f37e9-9844-45ae-b7ea-c19bd4aa4b58", string.Empty, string.Empty);

            // Assert
            StringAssert.Contains("\"tradeId\":\"558f37e9-9844-45ae-b7ea-c19bd4aa4b58\"", body);
            StringAssert.DoesNotContain("contractAddress", body);
            StringAssert.DoesNotContain("itemId", body);
        }

        [Test]
        public void SendOnlyTheItemPairForAMint()
        {
            // Act
            string body = MarketplaceCreditsAPIClient.BuildAuthorizeBody(130, string.Empty, "0x2222222222222222222222222222222222222222", "3");

            // Assert
            StringAssert.Contains("\"contractAddress\":\"0x2222222222222222222222222222222222222222\"", body);
            StringAssert.Contains("\"itemId\":\"3\"", body);
            // A mint has no trade, so the key is absent rather than empty.
            StringAssert.DoesNotContain("tradeId", body);
        }

        [Test]
        public void NeverSendAnEmptyIdentifier()
        {
            // Act
            string trade = MarketplaceCreditsAPIClient.BuildAuthorizeBody(110, "trade-1", null, null);
            string mint = MarketplaceCreditsAPIClient.BuildAuthorizeBody(130, null, "0x2222222222222222222222222222222222222222", "0");

            // Assert
            StringAssert.DoesNotContain("\"\"", trade);
            StringAssert.DoesNotContain("\"\"", mint);
        }

        /// <summary>Half a pair is not a pair: it must not be sent, because it resolves to nothing.</summary>
        [Test]
        public void FallBackToTheTradeShapeWhenTheItemPairIsIncomplete()
        {
            // Act
            string body = MarketplaceCreditsAPIClient.BuildAuthorizeBody(110, "trade-1", "0x2222222222222222222222222222222222222222", null);

            // Assert
            StringAssert.DoesNotContain("contractAddress", body);
            StringAssert.Contains("\"tradeId\":\"trade-1\"", body);
        }
    }
}
