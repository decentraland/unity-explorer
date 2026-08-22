using Cysharp.Threading.Tasks;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    // View-less tests: the controller's analytics relay must work for its whole lifetime, so it is
    // exercised without ever instantiating a view.
    public class CreditPurchaseModalControllerShould
    {
        private ICreditsPurchaseService purchaseService = null!;
        private CreditPurchaseModalController controller = null!;

        [SetUp]
        public void SetUp()
        {
            purchaseService = Substitute.For<ICreditsPurchaseService>();

            controller = new CreditPurchaseModalController(
                () => null!,
                purchaseService,
                Substitute.For<MarketplaceCreditsAPIClient>(null, null),
                Substitute.For<IWeb3IdentityCache>(),
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                _ => UniTask.CompletedTask,
                _ => UniTask.CompletedTask);
        }

        [TearDown]
        public void TearDown() =>
            controller.Dispose();

        [TestCase(CreditsPurchaseError.SignatureRejected, "user_rejected")]
        [TestCase(CreditsPurchaseError.Cancelled, "user_rejected")]
        [TestCase(CreditsPurchaseError.InsufficientCredits, "insufficient_credits")]
        [TestCase(CreditsPurchaseError.ListingNotAvailable, "not_for_sale")]
        [TestCase(CreditsPurchaseError.OwnListing, "not_for_sale")]
        [TestCase(CreditsPurchaseError.FeatureDisabled, "not_for_sale")]
        [TestCase(CreditsPurchaseError.PriceChanged, "price_error")]
        [TestCase(CreditsPurchaseError.PriceUnavailable, "price_error")]
        [TestCase(CreditsPurchaseError.SettlementPending, "settlement_pending")]
        [TestCase(CreditsPurchaseError.TransactionReverted, "transaction_failed")]
        [TestCase(CreditsPurchaseError.RelayerUnavailable, "service_unavailable")]
        [TestCase(CreditsPurchaseError.AuthorizationFailed, "service_unavailable")]
        [TestCase(CreditsPurchaseError.None, "unknown")]
        [TestCase(CreditsPurchaseError.SigningFailed, "unknown")]
        [TestCase(CreditsPurchaseError.EncodingFailed, "unknown")]
        [TestCase(CreditsPurchaseError.UnknownError, "unknown")]
        public void BucketEveryPurchaseErrorIntoAnAnalyticsErrorCode(CreditsPurchaseError error, string expectedBucket) =>
            Assert.AreEqual(expectedBucket, CreditPurchaseModalController.MapAnalyticsErrorCode(error));

        [Test]
        public void MapEveryPurchaseErrorToADistinctAnalyticsDetail()
        {
            // A newly added enum value falls into the switch default and collides with UnknownError,
            // failing this test until it gets an explicit mapping.
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (CreditsPurchaseError error in Enum.GetValues(typeof(CreditsPurchaseError)))
            {
                string detail = CreditPurchaseModalController.MapAnalyticsErrorDetail(error);
                Assert.IsNotEmpty(detail);
                Assert.IsTrue(seen.Add(detail), $"Duplicate analytics detail '{detail}' for {error}");
            }
        }

        [TestCase(CreditsPurchaseState.ResolvingListing, "resolving_listing")]
        [TestCase(CreditsPurchaseState.Authorizing, "authorizing")]
        [TestCase(CreditsPurchaseState.Signing, "signing")]
        [TestCase(CreditsPurchaseState.WaitingSettlement, "waiting_settlement")]
        [TestCase(CreditsPurchaseState.Success, "purchase")]
        [TestCase(CreditsPurchaseState.Failed, "purchase")]
        public void MapEveryPurchaseStateToAnAnalyticsStepName(CreditsPurchaseState state, string expectedStep) =>
            Assert.AreEqual(expectedStep, CreditPurchaseModalController.MapAnalyticsStepName(state));

        [Test]
        public void SurvivePurchaseStateChangesWithoutView()
        {
            // The step capture in OnPurchaseStateChanged sits above the view guard; it must not
            // throw for any state when no view was ever instantiated.
            foreach (CreditsPurchaseState state in Enum.GetValues(typeof(CreditsPurchaseState)))
                Assert.DoesNotThrow(() => purchaseService.StateChanged += Raise.Event<Action<CreditsPurchaseState>>(state));
        }
    }
}
