using DCL.MarketplaceCredits.Purchase;
using NUnit.Framework;
using System.Numerics;

namespace DCL.Shop.Tests
{
    public class ShopNameCardModelShould
    {
        private const string NFT_ID = "0x2a18-123";
        private const string NFT_URL = "/contracts/0x2a18/tokens/123";

        [Test]
        public void ShowTheNameWithItsPriceInMana()
        {
            // Arrange
            NameListingDto listing = Listing("Tinker", "50000000000000000000000");

            // Act
            ShopNameCardModel? model = ShopNameCardModel.FromListing(listing);

            // Assert
            Assert.IsNotNull(model);
            Assert.AreEqual(NFT_ID, model!.Key);
            Assert.AreEqual("Tinker", model.Name);
            Assert.AreEqual("50k", model.PriceText);
            Assert.AreEqual(NFT_URL, model.MarketplacePath);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("12.5")]
        [TestCase("-1000000000000000000")]
        public void SkipARowWithoutAUsablePrice(string? priceWei)
        {
            // Arrange
            NameListingDto listing = Listing("Tinker", priceWei);

            // Act
            ShopNameCardModel? model = ShopNameCardModel.FromListing(listing);

            // Assert
            Assert.IsNull(model);
        }

        [TestCase("0", "0")]
        [TestCase("250000000000000000000", "250")]
        [TestCase("500000000000000000", "0.5")]
        [TestCase("12250000000000000000", "12.25")]
        [TestCase("12059000000000000000", "12.05")]
        [TestCase("999999999999999999999", "999.99")]
        [TestCase("1500000000000000000000", "1.5k")]
        [TestCase("5000000000000000000000000", "5M")]
        public void FormatManaPrices(string priceWei, string expected)
        {
            // Act
            string text = ShopNameCardModel.FormatManaPrice(BigInteger.Parse(priceWei));

            // Assert
            Assert.AreEqual(expected, text);
        }

        [TestCase(ShopSortOption.Newest, false, NamesSort.Newest)]
        [TestCase(ShopSortOption.Newest, true, NamesSort.BestMatch)]
        [TestCase(ShopSortOption.Cheapest, false, NamesSort.Cheapest)]
        [TestCase(ShopSortOption.Cheapest, true, NamesSort.Cheapest)]
        [TestCase(ShopSortOption.MostExpensive, false, NamesSort.Newest)]
        [TestCase(ShopSortOption.Name, false, NamesSort.Newest)]
        public void MapTheHeaderSortToTheOrdersTheMarketplaceHasForNames(ShopSortOption sort, bool isSearching, NamesSort expected)
        {
            // Act
            NamesSort namesSort = ShopNamesResaleController.ToNamesSort(sort, isSearching);

            // Assert
            Assert.AreEqual(expected, namesSort);
        }

        private static NameListingDto Listing(string name, string? priceWei) =>
            new ()
            {
                nft = new NameNftDto { id = NFT_ID, name = name, url = NFT_URL },
                order = priceWei == null ? null : new NameOrderDto { price = priceWei },
            };
    }
}
