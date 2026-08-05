using Newtonsoft.Json;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     Parses a GET /v3/catalog/unified payload the way MarketplaceShopAPIClient does
    ///     (WRJsonParser.Newtonsoft), covering the native/legacy discrimination and the wire types.
    /// </summary>
    public class ShopListingDtoShould
    {
        // One native row and one legacy row, trimmed to the fields under test. The legacy price is the
        // documented 1 MANA case: about $0.069, so 0.69 credits, which the server rounds up to 1.
        private const string UNIFIED_PAYLOAD = @"{
            ""data"": [
                {
                    ""tradeId"": ""trade-native"",
                    ""listingType"": ""primary"",
                    ""source"": ""native"",
                    ""contractAddress"": ""0x2222222222222222222222222222222222222222"",
                    ""itemId"": ""3"",
                    ""name"": ""Cool Hat"",
                    ""thumbnail"": ""https://peer.decentraland.zone/hat.png"",
                    ""rarity"": ""epic"",
                    ""category"": ""wearable"",
                    ""creator"": ""0x4444444444444444444444444444444444444444"",
                    ""priceCredits"": 25,
                    ""manaWei"": null,
                    ""available"": 4,
                    ""network"": ""MATIC"",
                    ""chainId"": 80002
                },
                {
                    ""tradeId"": ""trade-legacy"",
                    ""listingType"": ""primary"",
                    ""source"": ""legacy"",
                    ""contractAddress"": ""0x5555555555555555555555555555555555555555"",
                    ""itemId"": ""9"",
                    ""name"": ""Old Boots"",
                    ""thumbnail"": ""https://peer.decentraland.zone/boots.png"",
                    ""rarity"": ""common"",
                    ""category"": ""wearable"",
                    ""creator"": ""0x6666666666666666666666666666666666666666"",
                    ""priceCredits"": 1,
                    ""manaWei"": ""1000000000000000000"",
                    ""available"": 1,
                    ""network"": ""MATIC"",
                    ""chainId"": 80002
                }
            ],
            ""total"": 58
        }";

        [Test]
        public void ParseNativeAndLegacyRowsFromOneFeed()
        {
            // Act
            ShopListingsResponse response = JsonConvert.DeserializeObject<ShopListingsResponse>(UNIFIED_PAYLOAD)!;

            // Assert
            Assert.IsNotNull(response.data);
            ShopListingDto[] data = response.data!;
            Assert.AreEqual(2, data.Length);
            Assert.AreEqual(58, response.total);

            ShopListingDto native = data[0];
            Assert.AreEqual("native", native.source);
            Assert.IsNull(native.manaWei);
            Assert.AreEqual("trade-native", native.tradeId);
            Assert.AreEqual(25, native.priceCredits);

            ShopListingDto legacy = data[1];
            Assert.AreEqual("legacy", legacy.source);
            Assert.AreEqual("1000000000000000000", legacy.manaWei);
            Assert.AreEqual("trade-legacy", legacy.tradeId);

            // The server rounds a sub-credit legacy price up to 1; nothing here recomputes it.
            Assert.AreEqual(1, legacy.priceCredits);
        }

        [Test]
        public void ParseManaPricesThatOverflowInt64()
        {
            // Arrange: 10 000 MANA in wei is 1e22, well past long.MaxValue (~9.22e18), so manaWei has to
            // stay a string — typing it as a numeric field makes this throw instead.
            const string HUGE_MANA_PAYLOAD = @"{
                ""data"": [{ ""tradeId"": ""trade-legacy"", ""source"": ""legacy"", ""priceCredits"": 4200, ""manaWei"": ""10000000000000000000000"" }],
                ""total"": 1
            }";

            // Act
            ShopListingsResponse response = JsonConvert.DeserializeObject<ShopListingsResponse>(HUGE_MANA_PAYLOAD)!;

            // Assert
            Assert.IsNotNull(response.data);
            ShopListingDto[] data = response.data!;
            Assert.AreEqual("10000000000000000000000", data[0].manaWei);
            Assert.AreEqual(4200, data[0].priceCredits);
        }
    }
}
