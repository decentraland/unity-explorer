using DCL.MarketplaceCredits.Purchase;
using NUnit.Framework;

namespace DCL.Shop.Tests
{
    public class ShopQueryMapperShould
    {
        [Test]
        public void UseTheUnifiedFeedWithThePriceRangeWhenOnSale()
        {
            // Arrange
            var filters = new ShopCollectiblesFilters { Category = ShopCategoryTree.WEARABLE, SubCategoryKey = "Head", MinPriceCredits = 5, MaxPriceCredits = 50, Smart = true, Sort = ShopSortOption.Cheapest };
            filters.ToggleRarity("mythic");
            filters.ToggleRarity("common");

            // Act
            ShopCatalogQuery query = ShopQueryMapper.ToQuery(filters, 96);

            // Assert
            Assert.IsTrue(filters.UsesUnifiedFeed);
            Assert.AreEqual(ShopQueryMapper.PAGE_SIZE, query.First);
            Assert.AreEqual(96, query.Skip);
            Assert.AreEqual(ShopItemCategory.Wearable, query.Category);
            Assert.AreEqual(6, query.WearableCategories!.Count);
            CollectionAssert.AreEqual(new[] { "common", "mythic" }, query.Rarities, "rarities keep the canonical order");
            Assert.AreEqual(5, query.MinPriceCredits);
            Assert.AreEqual(50, query.MaxPriceCredits);
            Assert.IsTrue(query.SmartOnly);
            Assert.IsNull(query.IsOnSale);
            Assert.AreEqual(ShopSort.Cheapest, query.Sort);
        }

        [Test]
        public void UseTheCatalogFeedWithoutPricesForAllAndNotForSale()
        {
            // Arrange
            var all = new ShopCollectiblesFilters { ExplicitStatus = ShopStatusFilter.All, MinPriceCredits = 5, Category = ShopCategoryTree.EMOTE, SubCategoryKey = "Dance" };
            var notForSale = new ShopCollectiblesFilters { ExplicitStatus = ShopStatusFilter.NotForSale };

            // Act
            ShopCatalogQuery allQuery = ShopQueryMapper.ToQuery(all, 0);
            ShopCatalogQuery notForSaleQuery = ShopQueryMapper.ToQuery(notForSale, 0);

            // Assert
            Assert.IsFalse(all.UsesUnifiedFeed);
            Assert.IsNull(allQuery.MinPriceCredits);
            Assert.IsNull(allQuery.IsOnSale);
            Assert.AreEqual(ShopItemCategory.Emote, allQuery.Category);
            CollectionAssert.AreEqual(new[] { "dance" }, allQuery.WearableCategories);
            Assert.AreEqual(false, notForSaleQuery.IsOnSale);
        }

        [Test]
        public void DefaultTheStatusToAllWhileSearchingUnlessPicked()
        {
            // Arrange
            var filters = new ShopCollectiblesFilters { SearchText = " hat " };

            // Act
            ShopStatusFilter whileSearching = filters.EffectiveStatus;
            filters.ExplicitStatus = ShopStatusFilter.OnSale;
            ShopStatusFilter picked = filters.EffectiveStatus;
            filters.SearchText = string.Empty;
            filters.ExplicitStatus = null;
            ShopStatusFilter idle = filters.EffectiveStatus;
            ShopCatalogQuery query = ShopQueryMapper.ToQuery(new ShopCollectiblesFilters { SearchText = " hat " }, 0);

            // Assert
            Assert.AreEqual(ShopStatusFilter.All, whileSearching);
            Assert.AreEqual(ShopStatusFilter.OnSale, picked);
            Assert.AreEqual(ShopStatusFilter.OnSale, idle);
            Assert.AreEqual("hat", query.Search);
        }

        [TestCase(ShopSortOption.Newest, "newest")]
        [TestCase(ShopSortOption.Cheapest, "price-asc")]
        [TestCase(ShopSortOption.MostExpensive, "price-desc")]
        [TestCase(ShopSortOption.Name, "name")]
        public void ReportTheWebSortKeysToAnalytics(ShopSortOption sort, string expected) =>
            Assert.AreEqual(expected, ShopQueryMapper.ToAnalyticsSort(sort));
    }
}
