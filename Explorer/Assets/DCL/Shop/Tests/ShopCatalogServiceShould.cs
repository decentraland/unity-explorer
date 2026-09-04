using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Purchase;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Shop.Tests
{
    public class ShopCatalogServiceShould
    {
        private const string COLLECTION = "0x2222222222222222222222222222222222222222";
        private const string OTHER_COLLECTION = "0x7777777777777777777777777777777777777777";

        private MarketplaceShopAPIClient api = null!;

        [SetUp]
        public void SetUp()
        {
            api = Substitute.For<MarketplaceShopAPIClient>(null, null);
            api.GetTrendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(new[] { Listing("1") }));
        }

        private static ShopListingDto Listing(string itemId) =>
            new () { tradeId = "trade-" + itemId, listingType = "primary", contractAddress = COLLECTION, itemId = itemId, name = "Item " + itemId, thumbnail = string.Empty, rarity = "epic", category = "wearable", creator = "0x44", priceCredits = 10, available = 3, chainId = 137 };

        private static CatalogItemDto CatalogItem(string collection, string itemId, int priceCredits = 10, int available = 3, string price = "1000000000000000000") =>
            new () { id = $"{collection}-{itemId}", name = "Item " + itemId, contractAddress = collection, category = "wearable", itemId = itemId, isOnSale = priceCredits > 0, price = price, priceCredits = priceCredits, available = available, chainId = 137 };

        private static OutfitDto Outfit(string id, params (string collection, string itemId)[] items)
        {
            var refs = new OutfitItemRefDto[items.Length];

            for (var i = 0; i < items.Length; i++)
                refs[i] = new OutfitItemRefDto { contractAddress = items[i].collection, itemId = items[i].itemId };

            return new OutfitDto { id = id, name = "Look " + id, thumbnailHash = new string('a', 64), items = refs, bodyShape = "unisex", gradientFrom = "#c640cd", gradientTo = "#691fa9", authorAddress = "0x44", published = true };
        }

        [Test]
        public async Task ServeTrendingFromTheCacheWithinTheTtlAndRefetchAfterInvalidation()
        {
            // Arrange
            var service = new ShopCatalogService(api, TimeSpan.FromMinutes(5));

            // Act
            IReadOnlyList<ShopItemCardModel> first = await service.GetTrendingAsync(CancellationToken.None);
            IReadOnlyList<ShopItemCardModel> second = await service.GetTrendingAsync(CancellationToken.None);
            service.Invalidate();
            await service.GetTrendingAsync(CancellationToken.None);

            // Assert
            Assert.AreSame(first, second);
            Assert.AreEqual(1, first.Count);
            await api.Received(2).GetTrendingAsync(ShopCatalogService.OVERVIEW_ROW_SIZE, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CoalesceConcurrentFetchesIntoOneRequest()
        {
            // Arrange
            var gate = new UniTaskCompletionSource<ShopListingDto[]>();
            api.GetTrendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
            var service = new ShopCatalogService(api, TimeSpan.FromMinutes(5));

            // Act
            UniTask<IReadOnlyList<ShopItemCardModel>> a = service.GetTrendingAsync(CancellationToken.None);
            UniTask<IReadOnlyList<ShopItemCardModel>> b = service.GetTrendingAsync(CancellationToken.None);
            gate.TrySetResult(new[] { Listing("1"), Listing("2") });
            IReadOnlyList<ShopItemCardModel> resultA = await a;
            IReadOnlyList<ShopItemCardModel> resultB = await b;

            // Assert
            Assert.AreEqual(2, resultA.Count);
            Assert.AreSame(resultA, resultB);
            await api.Received(1).GetTrendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task NotCacheAFailedFetch()
        {
            // Arrange
            var calls = 0;

            api.GetTrendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns(_ => ++calls == 1 ? UniTask.FromException<ShopListingDto[]>(new InvalidOperationException("down")) : UniTask.FromResult(new[] { Listing("1") }));

            var service = new ShopCatalogService(api, TimeSpan.FromMinutes(5));

            // Act
            var failed = false;

            try { await service.GetTrendingAsync(CancellationToken.None); }
            catch (InvalidOperationException) { failed = true; }

            IReadOnlyList<ShopItemCardModel> recovered = await service.GetTrendingAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(failed);
            Assert.AreEqual(1, recovered.Count);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public async Task AdmitOnlyOutfitsWhoseEveryItemIsBuyableFromItsCreator()
        {
            // Arrange: "full" resolves and is buyable, "soldout" has a piece with no stock, "missing" points at an unknown item.
            api.GetOutfitsAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(new[]
            {
                Outfit("full", (COLLECTION, "1"), (COLLECTION, "2")),
                Outfit("soldout", (COLLECTION, "1"), (OTHER_COLLECTION, "9")),
                Outfit("missing", (COLLECTION, "1"), (OTHER_COLLECTION, "404")),
            }));

            api.GetCatalogItemsByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(UniTask.FromResult(new[] { CatalogItem(COLLECTION, "1"), CatalogItem(COLLECTION, "2"), CatalogItem(OTHER_COLLECTION, "9", available: 0) }));

            api.OutfitThumbnailUrl(Arg.Any<string>()).Returns("https://shop-api.decentraland.org/v1/outfits/thumbnails/aaa");
            var service = new ShopCatalogService(api, TimeSpan.FromMinutes(5));

            // Act
            ShopOutfitsDataset dataset = await service.GetOutfitsAsync(CancellationToken.None);

            // Assert
            Assert.IsFalse(dataset.ResolutionFailed);
            Assert.AreEqual(1, dataset.Outfits.Count);
            Assert.AreEqual("full", dataset.Outfits[0].Id);
            Assert.AreEqual(20, dataset.Outfits[0].TotalCredits);
            Assert.AreEqual(2, dataset.Outfits[0].ResolvedItems.Count);
            await api.Received(1).GetCatalogItemsByIdsAsync(Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 4), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepEveryOutfitWhenTheCatalogueLookupFails()
        {
            // Arrange
            api.GetOutfitsAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult(new[] { Outfit("a", (COLLECTION, "1")), Outfit("b", (COLLECTION, "2")) }));

            api.GetCatalogItemsByIdsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
               .Returns(UniTask.FromException<CatalogItemDto[]>(new InvalidOperationException("down")));

            var service = new ShopCatalogService(api, TimeSpan.FromMinutes(5));

            // Act
            ShopOutfitsDataset dataset = await service.GetOutfitsAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(dataset.ResolutionFailed);
            Assert.AreEqual(2, dataset.Outfits.Count);
            Assert.AreEqual(0, dataset.Outfits[0].ResolvedItems.Count);
        }
    }
}
