using NUnit.Framework;

namespace DCL.Shop.Tests
{
    public class ShopGridFiltersShould
    {
        [Test]
        public void KeepTheSectionCategoryThroughResets()
        {
            // Arrange
            var filters = new ShopGridFilters(ShopCategoryTree.EMOTE) { SubCategoryKey = "Dance", SearchText = "wave", Sort = ShopSortOption.Name };
            filters.ToggleRarity("epic");

            // Act
            filters.ClearFilters();
            string categoryAfterClear = filters.Category;
            string searchAfterClear = filters.SearchText;
            filters.Reset();

            // Assert
            Assert.AreEqual(ShopCategoryTree.EMOTE, categoryAfterClear);
            Assert.AreEqual("wave", searchAfterClear, "clearing the filters keeps the search");
            Assert.AreEqual(ShopCategoryTree.EMOTE, filters.Category);
            Assert.IsNull(filters.SubCategoryKey);
            Assert.AreEqual(string.Empty, filters.SearchText);
            Assert.AreEqual(ShopSortOption.Newest, filters.Sort);
            Assert.IsEmpty(filters.Rarities);
        }

        [Test]
        public void KeepOneRarityAtATimeAndClearDealsOnReset()
        {
            // Arrange
            var filters = new ShopGridFilters(ShopCategoryTree.WEARABLE) { Deals = true };
            filters.SetSingleRarity("epic");

            // Act
            filters.SetSingleRarity("rare");
            string[] afterSecondPick = filters.Rarities.ToArray();
            filters.SetSingleRarity(null);
            int afterClear = filters.Rarities.Count;
            string signatureWithDeals = filters.BuildAnalyticsSignature();
            filters.Reset();

            // Assert
            CollectionAssert.AreEqual(new[] { "rare" }, afterSecondPick);
            Assert.AreEqual(0, afterClear);
            Assert.IsFalse(filters.Deals);
            Assert.AreNotEqual(signatureWithDeals, filters.BuildAnalyticsSignature(), "deals is part of the analytics signature");
        }

        [Test]
        public void KeepSearchAndSortPerInstance()
        {
            // Arrange
            var wearables = new ShopGridFilters(ShopCategoryTree.WEARABLE) { SearchText = "hat", Sort = ShopSortOption.Cheapest };
            var emotes = new ShopGridFilters(ShopCategoryTree.EMOTE);

            // Act
            emotes.SearchText = "dance";

            // Assert
            Assert.AreEqual("hat", wearables.SearchText);
            Assert.AreEqual(ShopSortOption.Cheapest, wearables.Sort);
            Assert.AreEqual("dance", emotes.SearchText);
            Assert.AreEqual(ShopSortOption.Newest, emotes.Sort);
        }
    }
}
