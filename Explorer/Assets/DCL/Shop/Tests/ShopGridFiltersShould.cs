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
