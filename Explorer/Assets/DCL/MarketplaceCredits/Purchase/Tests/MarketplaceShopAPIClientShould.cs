using DCL.Multiplayer.Connections.DecentralandUrls;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The wire contract of the shop reads: marketplace-server takes list filters as comma-separated values on
    ///     the unified feed but as repeated parameters on the catalog-items feed, and URLBuilder never escapes.
    /// </summary>
    public class MarketplaceShopAPIClientShould
    {
        private const string MARKETPLACE = "https://marketplace-api.decentraland.org";

        private IDecentralandUrlsSource urlsSource = null!;

        [SetUp]
        public void SetUp()
        {
            urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.MarketplaceServer).Returns(MARKETPLACE);
        }

        [Test]
        public void BuildTheUnifiedFeedUrlWithCsvListsAndItemGrouping()
        {
            // Arrange
            var query = new ShopCatalogQuery
            {
                First = 48,
                Skip = 96,
                Category = ShopItemCategory.Wearable,
                WearableCategories = new[] { "hat", "helmet" },
                Rarities = new[] { "epic", "mythic" },
                MinPriceCredits = 10,
                MaxPriceCredits = 500,
                Search = " cool hat ",
                Sort = ShopSort.MostExpensive,
                SmartOnly = true,
            };

            // Act
            string url = MarketplaceShopAPIClient.BuildShopItemsUrl(urlsSource, query);

            // Assert
            Assert.AreEqual(
                $"{MARKETPLACE}/v3/catalog/unified?first=48&skip=96&category=wearable&wearableCategory=hat,helmet&rarity=epic,mythic"
                + "&minPriceCredits=10&maxPriceCredits=500&search=cool%20hat&sortBy=most_expensive&isSmart=true&listingType=primary&groupBy=item",
                url);
        }

        [Test]
        public void BuildTheCatalogItemsUrlWithRepeatedListsAndNoPriceRange()
        {
            // Arrange
            var query = new ShopCatalogQuery
            {
                First = 48,
                Category = ShopItemCategory.Any,
                Rarities = new[] { "common", "rare" },
                MinPriceCredits = 10,
                Sort = ShopSort.Newest,
                SmartOnly = true,
                IsOnSale = false,
            };

            // Act
            string url = MarketplaceShopAPIClient.BuildCatalogItemsUrl(urlsSource, query);

            // Assert
            Assert.AreEqual(
                $"{MARKETPLACE}/v3/catalog/items?first=48&rarity=common&rarity=rare&sortBy=newest&isWearableSmart=true&isOnSale=false&includeSocialEmotes=false",
                url);
        }

        [Test]
        public void BuildTheTrendingAndByIdsUrls()
        {
            // Act
            string trending = MarketplaceShopAPIClient.BuildTrendingUrl(urlsSource, 12);
            string byIds = MarketplaceShopAPIClient.BuildCatalogItemsByIdsUrl(urlsSource, new[] { "0xaaaa-1", "0xbbbb-2", "0xcccc-3" }, 1, 2);

            // Assert
            Assert.AreEqual($"{MARKETPLACE}/v3/catalog/trending?first=12&includeSocialEmotes=false&listingType=primary", trending);
            Assert.AreEqual($"{MARKETPLACE}/v3/catalog/items?first=2&id=0xbbbb-2&id=0xcccc-3", byIds);
        }

        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", ExpectedResult = true)]
        [TestCase("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef", ExpectedResult = false)]
        [TestCase("0123456789abcdef", ExpectedResult = false)]
        [TestCase("", ExpectedResult = false)]
        [TestCase(null, ExpectedResult = false)]
        public bool AcceptOnlyLowercaseSha256HashesAsOutfitThumbnails(string? hash) =>
            MarketplaceShopAPIClient.IsOutfitThumbnailHash(hash);

        /// <summary>The credits-server answers 400 to a null optional key, so absent values must not be serialized at all.</summary>
        [Test]
        public void OmitAbsentOptionalKeysFromTheGroupAuthorizationBody()
        {
            // Arrange
            var lines = new[]
            {
                new CheckoutLine(250, "trade-1", "0x2222222222222222222222222222222222222222", "3"),
                new CheckoutLine(130, null, "0x7777777777777777777777777777777777777777", "7"),
                new CheckoutLine(90, "trade-2", null, null),
            };

            // Act
            JObject body = JObject.Parse(MarketplaceCreditsAPIClient.BuildAuthorizeGroupBody(lines));

            // Assert
            Assert.AreEqual("client", body["source"]!.ToString());
            var items = (JArray)body["items"]!;
            Assert.AreEqual(3, items.Count);
            Assert.AreEqual(250, (int)items[0]["usdPriceCents"]!);
            Assert.AreEqual("trade-1", items[0]["tradeId"]!.ToString());
            Assert.AreEqual("3", items[0]["itemId"]!.ToString());
            Assert.IsNull(items[1]["tradeId"]);
            Assert.AreEqual("7", items[1]["itemId"]!.ToString());
            Assert.IsNull(items[2]["contractAddress"]);
            Assert.IsNull(items[2]["itemId"]);
        }

        [TestCase(402L, CreditsAuthorizeError.InsufficientCredits)]
        [TestCase(409L, CreditsAuthorizeError.TooManyLiveIntents)]
        [TestCase(404L, CreditsAuthorizeError.FeatureDisabled)]
        [TestCase(400L, CreditsAuthorizeError.BadRequest)]
        [TestCase(500L, CreditsAuthorizeError.NetworkError)]
        public void MapGroupAuthorizationStatusCodes(long responseCode, CreditsAuthorizeError expected) =>
            Assert.AreEqual(expected, MarketplaceCreditsAPIClient.MapAuthorizeStatusCode(responseCode));
    }
}
