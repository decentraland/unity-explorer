using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class CreditPurchaseBuyHandlerShould
    {
        private const string ITEM_URN = "urn:decentraland:matic:collections-v2:0x2222222222222222222222222222222222222222:3";
        private const string MARKETPLACE_URL = "https://market.decentraland.org/contracts/0x2222222222222222222222222222222222222222/items/3";
        private const string SOURCE = CreditPurchaseModalControllerParams.SOURCE_PASSPORT_EQUIPPED;

        private IMVCManager mvcManager = null!;
        private MarketplaceShopAPIClient shopApiClient = null!;
        private MockWebBrowser webBrowser = null!;

        private readonly System.Collections.Generic.List<(string reason, string urn, string source)> fallbacks = new ();

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            shopApiClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            webBrowser = new MockWebBrowser();
            fallbacks.Clear();
        }

        private CreditPurchaseBuyHandler CreateHandler(bool isEnabled)
        {
            var handler = new CreditPurchaseBuyHandler(mvcManager, shopApiClient, webBrowser, isEnabled);
            handler.FellBackToWeb += (reason, urn, source) => fallbacks.Add((reason, urn, source));
            return handler;
        }

        private static CreditPurchaseBuyHandler.ItemVisuals CreateVisuals() =>
            new ("Cool Hat", "epic", null, null, Color.magenta, null);

        [Test]
        public async Task RedirectToWebWhenFeatureIsDisabled()
        {
            // Arrange
            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: false);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await shopApiClient.DidNotReceive().GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            CollectionAssert.AreEqual(new[] { (CreditPurchaseBuyHandler.FALLBACK_FEATURE_DISABLED, ITEM_URN, SOURCE) }, fallbacks);
        }

        [Test]
        public async Task OpenPurchaseModalWhenListingIsCreditBuyable()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync("0x2222222222222222222222222222222222222222", "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(new ShopListingDto { tradeId = "trade-1", priceCredits = 25 }));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(
                Arg.Is<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(cmd => cmd.InputData.Source == SOURCE && cmd.InputData.ItemUrn == ITEM_URN),
                Arg.Any<CancellationToken>());

            Assert.IsNull(webBrowser.UrlOpened);
            CollectionAssert.IsEmpty(fallbacks);
        }

        [Test]
        public async Task RedirectToWebWhenItemHasNoCreditListing()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(null));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>());
            CollectionAssert.AreEqual(new[] { (CreditPurchaseBuyHandler.FALLBACK_NO_CREDITS_LISTING, ITEM_URN, SOURCE) }, fallbacks);
        }

        [Test]
        public async Task RedirectToWebWhenListingResolutionFails()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromException<ShopListingDto?>(new Exception("boom")));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            CollectionAssert.AreEqual(new[] { (CreditPurchaseBuyHandler.FALLBACK_RESOLUTION_FAILED, ITEM_URN, SOURCE) }, fallbacks);
        }

        [Test]
        public async Task RedirectToWebWhenUrnIsNotParseable()
        {
            // Arrange
            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync("urn:decentraland:off-chain:base-avatars:brown_pants", MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await shopApiClient.DidNotReceive().GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            CollectionAssert.AreEqual(new[] { (CreditPurchaseBuyHandler.FALLBACK_NO_CREDITS_LISTING, "urn:decentraland:off-chain:base-avatars:brown_pants", SOURCE) }, fallbacks);
        }

        [Test]
        public async Task ResolveEachItemOnlyOncePerPassportOpening()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(new ShopListingDto { tradeId = "trade-1", priceCredits = 25 }));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            await shopApiClient.Received(1).GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

            // Act: clearing the cache forces a re-fetch (fresh listings on the next passport opening).
            handler.ClearCache();
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, _ => { }, CancellationToken.None);

            // Assert
            await shopApiClient.Received(2).GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task BracketResolutionWithResolvingCallback()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(null));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);
            var callbackValues = new System.Collections.Generic.List<bool>();

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), SOURCE, callbackValues.Add, CancellationToken.None);

            // Assert
            CollectionAssert.AreEqual(new[] { true, false }, callbackValues);
        }

        [Test]
        public void ParseContractAndItemFromCollectionUrns()
        {
            Assert.IsTrue(CreditPurchaseBuyHandler.TryParseCollectionItem(ITEM_URN, out string contract, out string itemId));
            Assert.AreEqual("0x2222222222222222222222222222222222222222", contract);
            Assert.AreEqual("3", itemId);

            Assert.IsFalse(CreditPurchaseBuyHandler.TryParseCollectionItem("urn:decentraland:off-chain:base-avatars:brown_pants", out _, out _));
            Assert.IsFalse(CreditPurchaseBuyHandler.TryParseCollectionItem("no-colons-here", out _, out _));
        }

        private class MockWebBrowser : UnityAppWebBrowser
        {
            public string? UrlOpened { get; private set; }

            public MockWebBrowser() : base(Substitute.For<IDecentralandUrlsSource>())
            {

            }

            public override void OpenUrlMainThreadOnly(string url) =>
                UrlOpened = url;

            public override void OpenUrlMainThreadOnly(DecentralandUrl url) =>
                UrlOpened = url.ToString();
        }
    }
}
