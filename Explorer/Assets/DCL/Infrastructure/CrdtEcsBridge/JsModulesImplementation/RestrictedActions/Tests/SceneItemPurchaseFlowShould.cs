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
using System.Threading;
using System.Threading.Tasks;

namespace CrdtEcsBridge.RestrictedActions.Tests
{
    public class SceneItemPurchaseFlowShould
    {
        private const string CONTRACT = "0x2222222222222222222222222222222222222222";
        private const string ITEM_URN = "urn:decentraland:matic:collections-v2:" + CONTRACT + ":3";

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

        [Test]
        public async Task FailWhenTheListingLookupThrows()
        {
            // Arrange
            shopApiClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns<UniTask<ShopListingDto?>>(_ => throw new InvalidOperationException("lookup failed"));

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
        public async Task ShowTheModalForTheListingWithVisualsLeftToTheModal()
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
                    && cmd.InputData.ItemName == "Cool Hat"
                    && cmd.InputData.RarityName == "epic"
                    && cmd.InputData.VisualsFromListing
                    && cmd.InputData.FallbackMarketplaceUrl == "https://example.org/marketplace/contracts/" + CONTRACT + "/items/3"),
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

        private void ArrangeListing() =>
            shopApiClient.GetShopListingForItemAsync(CONTRACT, "3", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(new ShopListingDto { tradeId = "trade-1", priceCredits = 25, name = "Cool Hat", rarity = "epic", category = "wearable", thumbnail = string.Empty }));
    }
}
