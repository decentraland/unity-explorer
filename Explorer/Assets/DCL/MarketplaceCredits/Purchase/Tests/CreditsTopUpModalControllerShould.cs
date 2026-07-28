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
        private CreditsTopUpModalController controller = null!;

        private readonly List<(string orderId, CreditPack pack)> redirected = new ();
        private readonly List<(string orderId, CreditPack pack)> completed = new ();
        private readonly List<CreditPack> pending = new ();
        private readonly List<(string step, string errorCode, CreditPack pack)> failed = new ();

        [SetUp]
        public void SetUp()
        {
            redirected.Clear();
            completed.Clear();
            pending.Clear();
            failed.Clear();

            topUpService = Substitute.For<ICreditsTopUpService>();

            controller = new CreditsTopUpModalController(
                () => null!,
                topUpService,
                Substitute.For<MarketplaceCreditsAPIClient>(null, null),
                Substitute.For<IWeb3IdentityCache>(),
                null!);

            controller.RedirectedToStripe += (orderId, pack) => redirected.Add((orderId, pack));
            controller.BuyCreditsCompleted += (orderId, pack) => completed.Add((orderId, pack));
            controller.BuyCreditsPending += pack => pending.Add(pack);
            controller.BuyCreditsFailed += (step, errorCode, pack) => failed.Add((step, errorCode, pack));
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

        private void RaiseStatus(CreditsTopUpStatus status) =>
            topUpService.StatusChanged += Raise.Event<Action<CreditsTopUpStatus>>(status);
    }
}
