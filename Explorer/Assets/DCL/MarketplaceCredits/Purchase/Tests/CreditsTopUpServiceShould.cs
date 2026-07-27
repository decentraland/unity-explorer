using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class CreditsTopUpServiceShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string ORDER_ID = "order-1";
        private const string CHECKOUT_URL = "https://checkout.stripe.com/c/pay/cs_test_123";

        private static readonly CreditPack PACK = CreditPackCatalog.PACKS[0];
        private static readonly TimeSpan WAIT_TIMEOUT = TimeSpan.FromSeconds(10);

        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private UnityAppWebBrowser webBrowser = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CreditsTopUpService service = null!;
        private List<CreditsTopUpStatus> recordedStatuses = null!;

        [SetUp]
        public void SetUp()
        {
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            webBrowser = Substitute.For<UnityAppWebBrowser>((IDecentralandUrlsSource)null!);
            identityCache = Substitute.For<IWeb3IdentityCache>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            creditsAPIClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(CheckoutSuccess());

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse()));

            CreateService(foregroundTimeoutMs: 5000, backgroundTimeoutMs: 5000);
        }

        [TearDown]
        public void TearDown() =>
            service.Dispose();

        [Test]
        public async Task OpenBrowserAndReachWaitingStateWhenCheckoutSucceeds()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Assert
            webBrowser.Received(1).OpenUrlMainThreadOnly(CHECKOUT_URL);
            Assert.AreEqual(ORDER_ID, service.CurrentStatus.OrderId);
            Assert.AreEqual(CreditsTopUpStage.CreatingCheckout, recordedStatuses[0].Stage);
        }

        [TestCase(CreditsCheckoutError.FeatureDisabled)]
        [TestCase(CreditsCheckoutError.PaymentsUnavailable)]
        [TestCase(CreditsCheckoutError.UnknownPack)]
        [TestCase(CreditsCheckoutError.ProviderError)]
        [TestCase(CreditsCheckoutError.NetworkError)]
        public async Task FailWithoutOpeningBrowserWhenCheckoutFails(CreditsCheckoutError checkoutError)
        {
            // Arrange
            creditsAPIClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(EnumResult<CheckoutResponse, CreditsCheckoutError>.ErrorResult(checkoutError, "boom")));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Assert
            Assert.AreEqual(checkoutError, service.CurrentStatus.CheckoutError);
            Assert.AreEqual("boom", service.CurrentStatus.ErrorMessage);
            webBrowser.DidNotReceive().OpenUrlMainThreadOnly(Arg.Any<string>());
        }

        [Test]
        public async Task ResetToIdleWhenCheckoutIsCancelled()
        {
            // Arrange
            creditsAPIClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(EnumResult<CheckoutResponse, CreditsCheckoutError>.ErrorResult(CreditsCheckoutError.Cancelled)));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Idle);

            // Assert
            webBrowser.DidNotReceive().OpenUrlMainThreadOnly(Arg.Any<string>());
        }

        [Test]
        public async Task TransitionToCreditedAndRefreshBalance()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(
                                 Order(CreditsOrderStatusResponse.STATUS_PROCESSING),
                                 Order(CreditsOrderStatusResponse.STATUS_PROCESSING),
                                 Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Credited);

            // Assert
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
            Assert.AreEqual(62, service.CurrentStatus.NewBalance);
            Assert.AreEqual(ORDER_ID, service.CurrentStatus.OrderId);
            await creditsAPIClient.Received(1).GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task TransitionToFailedWhenOrderFails()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_FAILED, error: "card_declined"));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Assert
            Assert.AreEqual("card_declined", service.CurrentStatus.ErrorMessage);
            Assert.IsNull(service.CurrentStatus.CheckoutError);
            await creditsAPIClient.DidNotReceive().GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnterPendingTimeoutWhenPollDeadlineElapses()
        {
            // Arrange
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 50);

            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.PendingTimeout);

            // Assert: the background window also expires and the soft state persists.
            await UniTask.Delay(200);
            Assert.AreEqual(CreditsTopUpStage.PendingTimeout, service.CurrentStatus.Stage);
        }

        [Test]
        public async Task ResolveLateCreditAfterPendingTimeout()
        {
            // Arrange
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 5000);

            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(_ => service.CurrentStatus.Stage == CreditsTopUpStage.PendingTimeout
                                 ? Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62)
                                 : Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Credited);

            // Assert
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
            await creditsAPIClient.Received(1).GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefreshBalanceSilentlyWhenPendingWasAcknowledged()
        {
            // Arrange
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 5000);

            var releaseCredit = false;

            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(_ => releaseCredit
                                 ? Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62)
                                 : Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.PendingTimeout);

            // Act
            service.AcknowledgeTerminalState();
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            releaseCredit = true;
            await UniTask.Delay(200);

            // Assert: the late grant refreshed the balance without re-surfacing a UI state.
            await creditsAPIClient.Received(1).GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
        }

        [Test]
        public async Task HandOffToBackgroundPollAndStillCreditWhenBrowserWaitStopped()
        {
            // Arrange: the order stays processing during the foreground wait and only completes once
            // it has been handed off to the background poll (mirroring a payment finished after the
            // user closed the browser tab). The foreground timeout is long, so the hand-off can only
            // come from StopWaitingForBrowser, not from a timeout.
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(_ => service.CurrentStatus.Stage == CreditsTopUpStage.PendingTimeout
                                 ? Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62)
                                 : Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Act
            service.StopWaitingForBrowser();

            // Assert
            await WaitForStageAsync(CreditsTopUpStage.Credited);
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
            Assert.IsTrue(recordedStatuses.Exists(s => s.Stage == CreditsTopUpStage.PendingTimeout));
        }

        [Test]
        public async Task IgnoreStopWaitingForBrowserWhenNotWaiting()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_FAILED, error: "card_declined"));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Act
            service.StopWaitingForBrowser();

            // Assert
            Assert.AreEqual(CreditsTopUpStage.Failed, service.CurrentStatus.Stage);
        }

        [Test]
        public async Task IgnoreStartTopUpWhileOrderInFlight()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Act
            service.StartTopUp(PACK);

            // Assert
            await creditsAPIClient.Received(1).CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SurvivePollErrorsUntilCredited()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(
                                 UniTask.FromResult(EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.ErrorResult(CreditsOrderPollError.NetworkError, "boom")),
                                 UniTask.FromResult(EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.ErrorResult(CreditsOrderPollError.NotFound, "not yet")),
                                 Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Credited);

            // Assert
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
        }

        [Test]
        public async Task StayCreditedWhenBalanceRefreshFails()
        {
            // Arrange
            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromException<UserCreditsResponse>(new Exception("boom")));

            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Credited);

            // Assert
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
        }

        [Test]
        public async Task ResetTerminalStateToIdleOnAcknowledge()
        {
            // Arrange
            creditsAPIClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_FAILED, error: "card_declined"));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Act
            service.AcknowledgeTerminalState();

            // Assert
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            Assert.IsFalse(service.IsOrderInFlight);
        }

        private void CreateService(double foregroundTimeoutMs, double backgroundTimeoutMs)
        {
            service?.Dispose();

            service = new CreditsTopUpService(creditsAPIClient, identityCache, webBrowser,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(foregroundTimeoutMs),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(backgroundTimeoutMs));

            recordedStatuses = new List<CreditsTopUpStatus>();
            service.StatusChanged += status => recordedStatuses.Add(status);
        }

        private async Task WaitForStageAsync(CreditsTopUpStage stage)
        {
            DateTime deadline = DateTime.UtcNow + WAIT_TIMEOUT;

            while (service.CurrentStatus.Stage != stage)
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"Timed out waiting for stage {stage}; current stage is {service.CurrentStatus.Stage}");

                await UniTask.Delay(5);
            }
        }

        private static UniTask<EnumResult<CheckoutResponse, CreditsCheckoutError>> CheckoutSuccess() =>
            UniTask.FromResult(EnumResult<CheckoutResponse, CreditsCheckoutError>.SuccessResult(new CheckoutResponse
            {
                orderId = ORDER_ID,
                url = CHECKOUT_URL,
            }));

        private static UniTask<EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>> Order(string status, int creditsGranted = 0, int newBalance = 0, string? error = null) =>
            UniTask.FromResult(EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError>.SuccessResult(new CreditsOrderStatusResponse
            {
                status = status,
                creditsGranted = creditsGranted,
                newBalance = newBalance,
                error = error!,
            }));
    }
}
