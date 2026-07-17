using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using JetBrains.Annotations;
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

        private IMVCManager mvcManager = null!;
        private MarketplaceShopAPIClient shopAPIClient = null!;
        private MockWebBrowser webBrowser = null!;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            shopAPIClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            webBrowser = new MockWebBrowser();
        }

        private CreditPurchaseBuyHandler CreateHandler(bool isEnabled) =>
            new (mvcManager, shopAPIClient, webBrowser, isEnabled);

        private static CreditPurchaseBuyHandler.ItemVisuals CreateVisuals() =>
            new ("Cool Hat", "epic", null, null, Color.magenta, null);

        [Test]
        public async Task RedirectToWebWhenFeatureIsDisabled()
        {
            // Arrange
            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: false);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await shopAPIClient.DidNotReceive().GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OpenPurchaseModalWhenListingIsCreditBuyable()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync("0x2222222222222222222222222222222222222222", "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(new ShopListingDto { tradeId = "trade-1", priceCredits = 25 }));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>());
            Assert.IsNull(webBrowser.UrlOpened);
        }

        [Test]
        public async Task RedirectToWebWhenItemHasNoCreditListing()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(null));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RedirectToWebWhenListingResolutionFails()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromException<ShopListingDto?>(new Exception("boom")));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
        }

        [Test]
        public async Task RedirectToWebWhenUrnIsNotParseable()
        {
            // Arrange
            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync("urn:decentraland:off-chain:base-avatars:brown_pants", MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            Assert.AreEqual(webBrowser.UrlOpened, MARKETPLACE_URL);
            await shopAPIClient.DidNotReceive().GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResolveEachItemOnlyOncePerPassportOpening()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(new ShopListingDto { tradeId = "trade-1", priceCredits = 25 }));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            await shopAPIClient.Received(1).GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

            // Act: clearing the cache forces a re-fetch (fresh listings on the next passport opening).
            handler.ClearCache();
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), _ => { }, CancellationToken.None);

            // Assert
            await shopAPIClient.Received(2).GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task BracketResolutionWithResolvingCallback()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(null));

            CreditPurchaseBuyHandler handler = CreateHandler(isEnabled: true);
            var callbackValues = new System.Collections.Generic.List<bool>();

            // Act
            await handler.HandleBuyClickAsync(ITEM_URN, MARKETPLACE_URL, CreateVisuals(), callbackValues.Add, CancellationToken.None);

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
            public string UrlOpened { get; private set; } = null;

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
