using NUnit.Framework;

namespace DCL.Shop.Tests
{
    public class ShopRaritySupplyShould
    {
        [TestCase("epic", "35", "EPIC 35/1000")]
        [TestCase("Mythic", "3", "MYTHIC 3/10")]
        [TestCase("unique", "1", "UNIQUE 1/1")]
        [TestCase("epic", null, "EPIC")]
        [TestCase("epic", "", "EPIC")]
        [TestCase("epic", "abc", "EPIC")]
        [TestCase("epic", "0", "EPIC")]
        [TestCase("heirloom", "5", "HEIRLOOM")]
        [TestCase("", "5", "")]
        public void LabelTheRarityWithTheIssueNumberWhenKnown(string rarity, string? issuedId, string expected) =>
            Assert.AreEqual(expected, ShopRaritySupply.ChipLabel(rarity, issuedId));

        [Test]
        public void KnowTheMaxSupplyOfEveryMarketplaceRarity()
        {
            // Arrange
            string[] rarities = { "unique", "mythic", "exotic", "legendary", "epic", "rare", "uncommon", "common" };
            int[] expected = { 1, 10, 50, 100, 1000, 5000, 10000, 100000 };

            // Act & Assert
            for (var i = 0; i < rarities.Length; i++)
                Assert.AreEqual(expected[i], ShopRaritySupply.MaxSupply(rarities[i]), rarities[i]);

            Assert.IsNull(ShopRaritySupply.MaxSupply(null));
        }
    }
}
