using DCL.MarketplaceCredits.Purchase.TopUp;
using DCL.MarketplaceCredits.Purchase.TopUp.UI;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    // View-less tests: the controller's analytics relay must work for its whole lifetime, so it is
    // exercised without ever instantiating a view.
    public class CreditsTopUpModalControllerShould
    {
        private const string ORDER_ID = "order-1";

        private static readonly CreditPack PACK = new ("pack_25", 24.99f, 235, true, string.Empty);

        private ICreditsTopUpService topUpService = null!;
        private TestableController controller = null!;

        private readonly List<(string orderId, CreditPack pack)> redirected = new ();
        private readonly List<(string orderId, CreditPack pack)> completed = new ();
        private readonly List<CreditPack> pending = new ();
        private readonly List<(string step, string errorCode, CreditPack pack)> failed = new ();
        private readonly List<(string orderId, CreditPack pack)> cancelled = new ();
        private readonly List<CreditPack> retried = new ();
        private readonly List<string> packsLoadFailed = new ();

        [SetUp]
        public void SetUp()
        {
            redirected.Clear();
            completed.Clear();
            pending.Clear();
            failed.Clear();
            cancelled.Clear();
            retried.Clear();
            packsLoadFailed.Clear();

            topUpService = Substitute.For<ICreditsTopUpService>();

            controller = new TestableController(
                topUpService,
                Substitute.For<MarketplaceCreditsAPIClient>(null, null),
                Substitute.For<IWeb3IdentityCache>());

            controller.RedirectedToStripe += (orderId, pack) => redirected.Add((orderId, pack));
            controller.BuyCreditsCompleted += (orderId, pack) => completed.Add((orderId, pack));
            controller.BuyCreditsPending += pack => pending.Add(pack);
            controller.BuyCreditsFailed += (step, errorCode, pack) => failed.Add((step, errorCode, pack));
            controller.BuyCreditsCancelled += (orderId, pack) => cancelled.Add((orderId, pack));
            controller.RetryClicked += pack => retried.Add(pack);
            controller.PacksLoadFailed += reason => packsLoadFailed.Add(reason);
        }

        [TearDown]
        public void TearDown() =>
            controller.Dispose();

        [Test]
        public void RelayFunnelEventsOnStageTransitions()
        {
            // Act
            RaiseStatus(CreditsTopUpStatus.CreatingCheckout(PACK));
            RaiseStatus(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));
            RaiseStatus(CreditsTopUpStatus.PendingTimeout(PACK, ORDER_ID));
            RaiseStatus(CreditsTopUpStatus.Credited(PACK, ORDER_ID, 250, 300));

            // Assert
            Assert.AreEqual(1, redirected.Count);
            Assert.AreEqual(ORDER_ID, redirected[0].orderId);
            Assert.AreEqual(PACK.Id, redirected[0].pack.Id);
            Assert.AreEqual(1, pending.Count);
            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual(ORDER_ID, completed[0].orderId);
            Assert.AreEqual(0, failed.Count);

            // The user-action relays must not fire from service stage transitions alone.
            Assert.AreEqual(0, cancelled.Count);
            Assert.AreEqual(0, retried.Count);
            Assert.AreEqual(0, packsLoadFailed.Count);
        }

        [Test]
        public void NotRelayDuplicateEventsWhenSameStageRepeats()
        {
            // Act
            RaiseStatus(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));
            RaiseStatus(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));

            // Assert
            Assert.AreEqual(1, redirected.Count);
        }

        [Test]
        public void RelayCheckoutFailureWithCheckoutStep()
        {
            // Act
            RaiseStatus(CreditsTopUpStatus.CheckoutFailed(PACK, CreditsCheckoutError.PaymentsUnavailable, "boom"));

            // Assert
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual("checkout", failed[0].step);
            Assert.AreEqual("payments_unavailable", failed[0].errorCode);
        }

        [Test]
        public void RelayGrantFailureWithGrantStep()
        {
            // Act
            RaiseStatus(CreditsTopUpStatus.GrantFailed(PACK, ORDER_ID, "card_declined"));

            // Assert
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual("grant", failed[0].step);
            Assert.AreEqual("grant_failed", failed[0].errorCode);
        }

        [Test]
        public void CancelTopUpWhenClosedWhileWaitingForBrowser()
        {
            // Arrange
            topUpService.CurrentStatus.Returns(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));
            controller.Show();
            RaiseStatus(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));

            // Act
            controller.Close();

            // Assert
            topUpService.Received(1).CancelTopUp();
            topUpService.DidNotReceive().AcknowledgeTerminalState();
            Assert.AreEqual(1, cancelled.Count);
            Assert.AreEqual(ORDER_ID, cancelled[0].orderId);
        }

        [Test]
        public void CancelTopUpWhenClosedWhilePending()
        {
            // Arrange
            topUpService.CurrentStatus.Returns(CreditsTopUpStatus.PendingTimeout(PACK, ORDER_ID));
            controller.Show();
            RaiseStatus(CreditsTopUpStatus.PendingTimeout(PACK, ORDER_ID));

            // Act
            controller.Close();

            // Assert
            topUpService.Received(1).CancelTopUp();
            topUpService.DidNotReceive().AcknowledgeTerminalState();
            Assert.AreEqual(1, cancelled.Count);
        }

        [Test]
        public void AcknowledgeWithoutCancellingWhenClosedAfterTerminalState()
        {
            // Arrange
            topUpService.CurrentStatus.Returns(CreditsTopUpStatus.Credited(PACK, ORDER_ID, 250, 300));
            controller.Show();
            RaiseStatus(CreditsTopUpStatus.Credited(PACK, ORDER_ID, 250, 300));

            // Act
            controller.Close();

            // Assert
            topUpService.Received(1).AcknowledgeTerminalState();
            topUpService.DidNotReceive().CancelTopUp();
            Assert.AreEqual(0, cancelled.Count);
        }

        [Test]
        public void StartFromPackSelectionWhenReopenedAfterCancellingClose()
        {
            // Arrange: first close happens mid browser wait and cancels the top-up.
            topUpService.CurrentStatus.Returns(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));
            controller.Show();
            RaiseStatus(CreditsTopUpStatus.WaitingForPayment(PACK, ORDER_ID));
            controller.Close();

            // Act: the service is idle again, so reopening and closing must not cancel anything else.
            topUpService.CurrentStatus.Returns(CreditsTopUpStatus.Idle());
            controller.Show();
            controller.Close();

            // Assert
            topUpService.Received(1).CancelTopUp();
            topUpService.DidNotReceive().AcknowledgeTerminalState();
            Assert.AreEqual(1, cancelled.Count);
        }

        private void RaiseStatus(CreditsTopUpStatus status) =>
            topUpService.StatusChanged += Raise.Event<Action<CreditsTopUpStatus>>(status);

        // Exposes the protected view lifecycle so close behavior is testable without a view instance.
        private class TestableController : CreditsTopUpModalController
        {
            public TestableController(
                ICreditsTopUpService topUpService,
                MarketplaceCreditsAPIClient creditsApiClient,
                IWeb3IdentityCache identityCache)
                : base(() => null!, topUpService, creditsApiClient, identityCache, null!) { }

            public void Show() =>
                OnViewShow();

            public void Close() =>
                OnViewClose();
        }
    }
}
