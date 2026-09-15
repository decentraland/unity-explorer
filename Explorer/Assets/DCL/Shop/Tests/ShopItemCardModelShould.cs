using DCL.MarketplaceCredits.Purchase;
using NUnit.Framework;

namespace DCL.Shop.Tests
{
    public class ShopItemCardModelShould
    {
        private const string COLLECTION = "0x2222222222222222222222222222222222222222";

        private static ShopListingDto Listing(int priceCredits = 25, int available = 4, string? tokenId = null, int? compareAt = null, long? saleEndsAt = null) =>
            new ()
            {
                tradeId = "trade-1",
                listingType = tokenId == null ? "primary" : "secondary",
                contractAddress = COLLECTION.ToUpperInvariant().Replace("0X", "0x"),
                itemId = "3",
                tokenId = tokenId,
                name = "Cool Hat",
                thumbnail = "https://peer.decentraland.org/hat.png",
                rarity = "epic",
                category = "wearable",
                wearableCategory = "hat",
                creator = "0x4444444444444444444444444444444444444444",
                priceCredits = priceCredits,
                available = available,
                chainId = 80002,
                compareAtCredits = compareAt,
                saleEndsAt = saleEndsAt,
            };

        [Test]
        public void TreatOnlyAZeroPriceOrAnExhaustedSupplyAsNotForSale()
        {
            // Act
            ShopItemCardModel priced = ShopItemCardModel.FromListing(Listing());
            ShopItemCardModel free = ShopItemCardModel.FromListing(Listing(priceCredits: 0));
            ShopItemCardModel soldOut = ShopItemCardModel.FromListing(Listing(available: 0));
            ShopItemCardModel token = ShopItemCardModel.FromListing(Listing(available: 0, tokenId: "105"));

            // Assert
            Assert.IsFalse(priced.IsNotForSale);
            Assert.IsTrue(free.IsNotForSale);
            Assert.IsTrue(soldOut.IsNotForSale);
            Assert.IsFalse(token.IsNotForSale, "a resold token has no supply to run out of");
            Assert.IsNull(token.Available);
        }

        [Test]
        public void ActivateASaleOnlyWithAHigherStrikePriceAndAFutureEnd()
        {
            // Arrange
            const long NOW = 1_800_000_000;

            // Act
            ShopItemCardModel live = ShopItemCardModel.FromListing(Listing(compareAt: 50, saleEndsAt: NOW + 3600));
            ShopItemCardModel ended = ShopItemCardModel.FromListing(Listing(compareAt: 50, saleEndsAt: NOW - 1));
            ShopItemCardModel cheaperBefore = ShopItemCardModel.FromListing(Listing(compareAt: 20));

            // Assert
            Assert.IsTrue(live.IsSaleActive(NOW));
            Assert.AreEqual(50, live.DiscountPercent());
            Assert.IsFalse(ended.IsSaleActive(NOW));
            Assert.IsFalse(cheaperBefore.IsSaleActive(NOW));
            Assert.AreEqual(0, cheaperBefore.DiscountPercent());
        }

        [Test]
        public void BuildTheUrnFromTheItemChainAndLowercaseTheContract()
        {
            // Act
            ShopItemCardModel amoy = ShopItemCardModel.FromListing(Listing());
            ShopListingDto polygonListing = Listing();
            polygonListing.chainId = 137;
            ShopItemCardModel polygon = ShopItemCardModel.FromListing(polygonListing);

            // Assert
            Assert.AreEqual($"urn:decentraland:amoy:collections-v2:{COLLECTION}:3", amoy.Urn);
            Assert.AreEqual($"urn:decentraland:matic:collections-v2:{COLLECTION}:3", polygon.Urn);
            Assert.AreEqual(COLLECTION, amoy.ContractAddress);
            Assert.AreEqual("trade-1", amoy.Key);
        }

        [Test]
        public void MapCatalogRowsWithoutAListing()
        {
            // Arrange
            var dto = new CatalogItemDto
            {
                id = $"{COLLECTION}-3",
                name = "Cool Hat",
                contractAddress = COLLECTION,
                category = "wearable",
                itemId = "3",
                chainId = 137,
                isOnSale = true,
                price = "20000000000000000000",
                priceCredits = 14,
                available = 2,
                data = new CatalogItemDataDto { wearable = new CatalogWearableDataDto { category = "hat", bodyShapes = new[] { "urn:decentraland:off-chain:base-avatars:BaseMale", "urn:decentraland:off-chain:base-avatars:BaseFemale" }, isSmart = true } },
            };

            // Act
            ShopItemCardModel model = ShopItemCardModel.FromCatalogItem(dto);

            // Assert
            Assert.IsNull(model.Listing);
            Assert.AreEqual("hat", model.WearableCategory);
            Assert.AreEqual("unisex", model.Gender);
            Assert.IsTrue(model.IsSmart);
            Assert.IsTrue(model.HasCreatorMint);
            Assert.AreEqual(14, model.PriceCredits);
            Assert.AreEqual(2, model.Available);
            Assert.AreEqual($"{COLLECTION}-3", model.Key);
        }

        [TestCase(0L, "")]
        [TestCase(45L, "45s")]
        [TestCase(750L, "12m 30s")]
        [TestCase(15120L, "4h 12m")]
        [TestCase(187200L, "2d 4h")]
        public void FormatTheSaleCountdownLikeTheWeb(long secondsLeft, string expected) =>
            Assert.AreEqual(expected, ShopItemCardModel.FormatCountdown(secondsLeft));
    }
}
