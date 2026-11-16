using Cysharp.Threading.Tasks;
using DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using ECS.Unity.ExplorerUiEvents;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    public class SceneItemPurchaseFlowShould
    {
        private const string CONTRACT = "0x2222222222222222222222222222222222222222";
        private const string ITEM_URN = "urn:decentraland:matic:collections-v2:" + CONTRACT + ":3";
        private const int POLYGON_CHAIN_ID = 137;
        private const int AMOY_CHAIN_ID = 80002;

        private IMVCManager mvcManager = null!;
        private MarketplaceShopAPIClient shopApiClient = null!;
        private SceneItemPurchaseFlow flow = null!;

        [SetUp]
        public void SetUp()
        {
            mvcManager = Substitute.For<IMVCManager>();
            shopApiClient = Substitute.For<MarketplaceShopAPIClient>(null, null);

            var urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.MarketplaceLink).Returns("https://example.org/marketplace");

            // The listings below carry no thumbnail, so the image provider never reaches its world.
            flow = new SceneItemPurchaseFlow(mvcManager, shopApiClient, new ImageControllerProvider(null!), urlsSource);
        }

        [TearDown]
        public void TearDown() =>
            flow.Dispose();

        [TestCase("not-a-urn")]
        [TestCase("")]
        [TestCase("urn:decentraland:matic:collections-v2:" + CONTRACT + ":3:105312291668557186697918027683670432318895095400549111254310977537")]
        [TestCase("urn:x:" + CONTRACT + "&listingType=secondary&first=5:0")]
        [TestCase("urn:decentraland:matic:collections-v2:" + CONTRACT + "&listingType=secondary:3")]
        [TestCase("urn:decentraland:matic:collections-thirdparty:a:b:c:" + CONTRACT + ":3")]
        [TestCase("urn:decentraland:mainnet:collections-v2:" + CONTRACT + ":3")]
        [TestCase("urn:decentraland:matic:collections-v2:0x2222:3")]
        [TestCase("urn:decentraland:matic:collections-v2:" + CONTRACT + ":+3")]
        [TestCase("urn:decentraland:matic:collections-v2:" + CONTRACT + ":3\n")]
        [TestCase(" urn:decentraland:matic:collections-v2:" + CONTRACT + ":3")]
        public async Task FailWithoutALookupWhenTheUrnIsNotACollectionItem(string urn)
        {
            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(urn, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Failed, status);
            await shopApiClient.DidNotReceive().GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWithoutAModalWhenTheItemHasNoListing()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(CONTRACT, "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(null));

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Failed, status);
            await mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>());
        }

        [TestCase("0x3333333333333333333333333333333333333333", "3", POLYGON_CHAIN_ID)]
        [TestCase(CONTRACT, "4", POLYGON_CHAIN_ID)]
        [TestCase(CONTRACT, null, POLYGON_CHAIN_ID)]
        [TestCase(CONTRACT, "3", 1)]
        public async Task FailWithoutAModalWhenTheListingIsNotTheOfferedItem(string listingContract, string? listingItemId, int listingChainId)
        {
            // Arrange
            ArrangeListing(listingContract, listingItemId, listingChainId);

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Failed, status);
            await mvcManager.DidNotReceive().ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWhenTheListingLookupThrows()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(_ => throw new InvalidOperationException("lookup failed"));

            LogAssert.Expect(LogType.Exception, new Regex("lookup failed"));

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Failed, status);
        }

        [TestCase(CreditPurchaseModalOutcome.Purchased, ExplorerItemPurchaseStatus.Purchased)]
        [TestCase(CreditPurchaseModalOutcome.Dismissed, ExplorerItemPurchaseStatus.Dismissed)]
        [TestCase(CreditPurchaseModalOutcome.Failed, ExplorerItemPurchaseStatus.Failed)]
        public async Task ReportHowTheModalEnded(CreditPurchaseModalOutcome outcome, ExplorerItemPurchaseStatus expected)
        {
            // Arrange
            ArrangeListing();

            mvcManager.ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>())
                      .Returns(info =>
                       {
                           info.ArgAt<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(0).InputData.Closed!.Invoke(outcome);
                           return UniTask.CompletedTask;
                       });

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(expected, status);
        }

        [Test]
        public async Task ShowTheModalForTheListing()
        {
            // Arrange
            ArrangeListing();

            // Act
            await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(
                Arg.Is<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(cmd =>
                    cmd.InputData.Source == CreditPurchaseModalControllerParams.SOURCE_SDK_SCENE
                    && cmd.InputData.ItemUrn == ITEM_URN
                    && cmd.InputData.Listing.name == "Cool Hat"
                    && cmd.InputData.FallbackMarketplaceUrl == "https://example.org/marketplace/contracts/" + CONTRACT + "/items/3"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task GiveTheModalTheUrnOfTheListingNotTheOneTheSceneSent()
        {
            // Arrange: the scene spells the address in upper case, the listing in lower case.
            const string LISTING_CONTRACT = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            const string SCENE_CONTRACT = "0xABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD";

            shopApiClient.GetShopListingForItemAsync(SCENE_CONTRACT, "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(CreateListing(LISTING_CONTRACT, "3", AMOY_CHAIN_ID)));

            // Act
            await flow.RunAsync("urn:decentraland:matic:collections-v2:" + SCENE_CONTRACT + ":3", CancellationToken.None);

            // Assert
            await mvcManager.Received(1).ShowAsync(
                Arg.Is<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(cmd =>
                    cmd.InputData.ItemUrn == "urn:decentraland:amoy:collections-v2:" + LISTING_CONTRACT + ":3"),
                Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWhenTheModalNeverRan()
        {
            // Arrange: MVC returns from a show request at once, without running the modal, when its
            // controller is already on screen.
            ArrangeListing();

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Failed, status);
        }

        [Test]
        public async Task ReportDismissedWhenTheShowIsCancelled()
        {
            // Arrange
            ArrangeListing();

            mvcManager.ShowAsync(Arg.Any<ShowCommand<CreditPurchaseModalView, CreditPurchaseModalControllerParams>>(), Arg.Any<CancellationToken>())
                      .Returns(UniTask.FromCanceled());

            // Act
            ExplorerItemPurchaseStatus status = await flow.RunAsync(ITEM_URN, CancellationToken.None);

            // Assert
            Assert.AreEqual(ExplorerItemPurchaseStatus.Dismissed, status);
        }

        [Test]
        public void HoldTheSlotUntilItIsReleased()
        {
            // Act and assert
            Assert.IsTrue(flow.TryReserve());
            Assert.IsFalse(flow.TryReserve());

            flow.Release();

            Assert.IsTrue(flow.TryReserve());
        }

        [Test]
        public void RefuseTheSlotWhileThePurchaseModalIsOnScreen()
        {
            // Arrange
            mvcManager.IsShowing<CreditPurchaseModalView, CreditPurchaseModalControllerParams>().Returns(true);

            // Act and assert
            Assert.IsFalse(flow.TryReserve());
        }

        private void ArrangeListing(string listingContract = CONTRACT, string? listingItemId = "3", int listingChainId = POLYGON_CHAIN_ID) =>
            shopApiClient.GetShopListingForItemAsync(CONTRACT, "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(CreateListing(listingContract, listingItemId, listingChainId)));

        private static ShopListingDto CreateListing(string contract, string? itemId, int chainId) =>
            new () { tradeId = "trade-1", contractAddress = contract, itemId = itemId, chainId = chainId, priceCredits = 25, name = "Cool Hat", rarity = "epic", category = "wearable", thumbnail = string.Empty };
    }
}
