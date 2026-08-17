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

        private static readonly CreditPack PACK = new ("pack_5", 4.99f, 45, false, string.Empty);
        private static readonly TimeSpan WAIT_TIMEOUT = TimeSpan.FromSeconds(10);

        private MarketplaceCreditsAPIClient creditsApiClient = null!;
        private UnityAppWebBrowser webBrowser = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CreditsTopUpService service = null!;
        private List<CreditsTopUpStatus> recordedStatuses = null!;

        [SetUp]
        public void SetUp()
        {
            creditsApiClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            webBrowser = Substitute.For<UnityAppWebBrowser>((IDecentralandUrlsSource)null!);
            identityCache = Substitute.For<IWeb3IdentityCache>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            creditsApiClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(CheckoutSuccess());

            creditsApiClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
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
            creditsApiClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            creditsApiClient.CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
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
            await creditsApiClient.Received(1).GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task TransitionToFailedWhenOrderFails()
        {
            // Arrange
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_FAILED, error: "card_declined"));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Assert
            Assert.AreEqual("card_declined", service.CurrentStatus.ErrorMessage);
            Assert.IsNull(service.CurrentStatus.CheckoutError);
            await creditsApiClient.DidNotReceive().GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EnterPendingTimeoutWhenPollDeadlineElapses()
        {
            // Arrange
            service.Dispose();
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 5000);

            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.PendingTimeout);

            // Assert: the soft state persists while the background window is still open.
            await UniTask.Delay(200);
            Assert.AreEqual(CreditsTopUpStage.PendingTimeout, service.CurrentStatus.Stage);
        }

        [Test]
        public async Task FailTerminallyWhenBackgroundPollAlsoTimesOut()
        {
            // Arrange
            service.Dispose();
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 50);

            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Failed);

            // Assert: the pending spinner resolved through PendingTimeout into a terminal grant
            // failure - never an eternal soft state.
            Assert.IsTrue(recordedStatuses.Exists(static status => status.Stage == CreditsTopUpStage.PendingTimeout));
            Assert.IsNull(service.CurrentStatus.CheckoutError);
            Assert.AreEqual(ORDER_ID, service.CurrentStatus.OrderId);
            Assert.IsNotNull(service.CurrentStatus.ErrorMessage);
        }

        [Test]
        public async Task ResolveLateCreditAfterPendingTimeout()
        {
            // Arrange
            service.Dispose();
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 5000);

            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(_ => service.CurrentStatus.Stage == CreditsTopUpStage.PendingTimeout
                                 ? Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62)
                                 : Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.Credited);

            // Assert
            Assert.AreEqual(50, service.CurrentStatus.CreditsGranted);
            await creditsApiClient.Received(1).GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResetToIdleAndStopPollingWhenCancelledWhileWaitingForPayment()
        {
            // Arrange
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Act
            service.CancelTopUp();

            // Assert
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            Assert.IsFalse(service.IsOrderInFlight);

            // A grant landing after the cancel must be ignored: the poll is dead.
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62));

            await UniTask.Delay(200);
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            await creditsApiClient.DidNotReceive().GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ResetToIdleAndStopBackgroundPollWhenCancelledWhilePending()
        {
            // Arrange
            service.Dispose();
            CreateService(foregroundTimeoutMs: 30, backgroundTimeoutMs: 5000);

            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.PendingTimeout);

            // Act
            service.CancelTopUp();

            // Assert
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);

            // A grant landing after the cancel must be ignored: the background poll is dead.
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_CREDITED, creditsGranted: 50, newBalance: 62));

            await UniTask.Delay(200);
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            await creditsApiClient.DidNotReceive().GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void IgnoreCancelTopUpWhenIdle()
        {
            // Act
            service.CancelTopUp();

            // Assert
            Assert.AreEqual(CreditsTopUpStage.Idle, service.CurrentStatus.Stage);
            Assert.AreEqual(0, recordedStatuses.Count);
        }

        [Test]
        public async Task StartFreshTopUpAfterCancel()
        {
            // Arrange
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);
            service.CancelTopUp();

            // Act
            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Assert
            await creditsApiClient.Received(2).CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task IgnoreStartTopUpWhileOrderInFlight()
        {
            // Arrange
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
                            .Returns(Order(CreditsOrderStatusResponse.STATUS_PROCESSING));

            service.StartTopUp(PACK);
            await WaitForStageAsync(CreditsTopUpStage.WaitingForPayment);

            // Act
            service.StartTopUp(PACK);

            // Assert
            await creditsApiClient.Received(1).CreateCheckoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SurvivePollErrorsUntilCredited()
        {
            // Arrange
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
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
            creditsApiClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromException<UserCreditsResponse>(new Exception("boom")));

            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
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
            creditsApiClient.GetCheckoutOrderAsync(ORDER_ID, Arg.Any<CancellationToken>())
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
            service = new CreditsTopUpService(creditsApiClient, identityCache, webBrowser,
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
