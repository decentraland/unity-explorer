using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The cart charge: one credit and one signature per transaction group, settled in order, with the
    ///     web shop's release rules (never touch a broadcast group's credit, release the never-signed rest).
    /// </summary>
    public class CreditsCartCheckoutServiceShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string SELLER = "0x24e5f44999c151f08609f8e27b2238c773c4d020";
        private const string TRADE_ID = "trade-1";
        private const string GREEDY_TRADE_ID = "trade-2";
        private const string TRADE_COLLECTION = "0x2222222222222222222222222222222222222222";
        private const string MINT_COLLECTION_A = "0x7777777777777777777777777777777777777777";
        private const string MINT_COLLECTION_B = "0x8888888888888888888888888888888888888888";
        private const string CREDIT_TRADE = "credit-trade";
        private const string CREDIT_STORE = "credit-store";
        private const string TX_HASH_1 = "0x1111";
        private const string TX_HASH_2 = "0x2222";

        // 5 MANA at $0.25 per MANA is $1.25, charged as 13 whole credits.
        private const string MINT_MANA_WEI = "5000000000000000000";
        private const int MINT_PRICE_CENTS = 130;
        private const int MINT_PRICE_CREDITS = 13;

        // A USD-pegged trade at exactly $2.50.
        private const string TRADE_USD_WEI = "2500000000000000000";
        private const int TRADE_PRICE_CENTS = 250;
        private const int TRADE_PRICE_CREDITS = 25;

        private const int ORACLE_DECIMALS = 8;
        private const int MANA_USD_RATE = 25_000_000;
        private const string HALF_MANA_WEI = "500000000000000000";

        // Plenty for every group: 100 MANA.
        private const string GROUP_CAP_WEI = "100000000000000000000";

        private static readonly BigInteger ONE_MANA = BigInteger.Parse("1000000000000000000");
        private static readonly BigInteger HUNDRED_MANA = 100 * ONE_MANA;

        private MarketplaceShopAPIClient shopAPIClient = null!;
        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private MetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
        private ManaTokenReader manaTokenReader = null!;
        private CreditsChainConfig chainConfig = null!;
        private ICreditsPurchaseService quoteService = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CancellationTokenSource warmUpCts = null!;
        private CancellationTokenSource uiCts = null!;
        private ShopCart cart = null!;
        private RelayedPurchaseGate purchaseGate = null!;
        private CreditsCartCheckoutService service = null!;

        [SetUp]
        public void SetUp()
        {
            shopAPIClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            metaTxRelayer = Substitute.For<MetaTxRelayer>(null, null, null, null);
            settlementPoller = Substitute.For<PolygonSettlementPoller>(null, null);
            manaUsdRateReader = Substitute.For<ManaUsdRateReader>(null, null);
            quoteService = Substitute.For<ICreditsPurchaseService>();
            identityCache = Substitute.For<IWeb3IdentityCache>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            quoteService.QuoteAsync(Arg.Any<ShopListingDto>(), Arg.Any<CancellationToken>())
                        .Returns(call => UniTask.FromResult(QuoteFor(call.Arg<ShopListingDto>())));

            shopAPIClient.GetShopListingForItemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(call => UniTask.FromResult<ShopListingDto?>(MintListing(call.ArgAt<string>(0), call.ArgAt<string>(1))));

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromResult(new ManaUsdRate(MANA_USD_RATE, ORACLE_DECIMALS)));

            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>())
                            .Returns(call => UniTask.FromResult(Authorized(call.Arg<IReadOnlyList<CheckoutLine>>())));

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH_1, nonce: new BigInteger(5))));

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign >= 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH_2, nonce: new BigInteger(6))));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

            warmUpCts = new CancellationTokenSource();
            uiCts = new CancellationTokenSource();

            cart = new ShopCart(identityCache);
            cart.Add(TradeListing(), ShopCartSource.Grid);
            cart.Add(MintListing(MINT_COLLECTION_A, "1"), ShopCartSource.Outfit, "outfit-1");
            cart.Add(MintListing(MINT_COLLECTION_A, "1"), ShopCartSource.Outfit, "outfit-1");
            cart.Add(MintListing(MINT_COLLECTION_B, "2"), ShopCartSource.Grid);

            chainConfig = new CreditsChainConfig(EthereumNetwork.Sepolia);
            manaTokenReader = Substitute.For<ManaTokenReader>(null, null);

            purchaseGate = new RelayedPurchaseGate();

            service = new CreditsCartCheckoutService(cart, quoteService, shopAPIClient, creditsAPIClient, new UseCreditsExecutor(creditsAPIClient, metaTxRelayer, settlementPoller, chainConfig),
                new ManaRail(manaTokenReader, metaTxRelayer, settlementPoller, chainConfig), new PaymentOptionsProvider(creditsAPIClient, manaTokenReader, manaUsdRateReader, chainConfig, true),
                purchaseGate, manaUsdRateReader, chainConfig, identityCache, new CreditsFeatureAccess(identityCache, warmUpCts.Token), true);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            cart.Dispose();
            warmUpCts.Cancel();
            warmUpCts.Dispose();
            uiCts.Dispose();
            FeatureFlagsConfiguration.Reset();
        }

        private static ShopListingDto TradeListing() =>
            new ()
            {
                tradeId = TRADE_ID,
                acquisition = "trade",
                listingType = "primary",
                source = "native",
                contractAddress = TRADE_COLLECTION,
                itemId = "3",
                priceCredits = TRADE_PRICE_CREDITS,
                available = 5,
                chainId = 80002,
            };

        private static ShopListingDto MintListing(string collection, string itemId, int available = 8) =>
            new ()
            {
                tradeId = null!,
                acquisition = "store",
                listingType = "primary",
                source = "legacy",
                contractAddress = collection,
                itemId = itemId,
                manaWei = MINT_MANA_WEI,
                priceCredits = MINT_PRICE_CREDITS,
                available = available,
                chainId = 80002,
            };

        private static TradeDto CreateTrade() =>
            new ()
            {
                id = TRADE_ID,
                signer = SELLER,
                signature = "0x" + new string('a', 130),
                type = "public_item_order",
                network = "matic",
                chainId = 80002,
                contract = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                checks = new TradeChecksDto { uses = 1, expiration = 1893456000000, effective = 1735689600000, salt = "0x1234", allowedRoot = "0x" },
                sent = new[] { new TradeAssetDto { assetType = CreditsTradeEncoder.ASSET_TYPE_COLLECTION_ITEM, contractAddress = TRADE_COLLECTION, itemId = "3" } },
                received = new[] { new TradeAssetDto { assetType = CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA, contractAddress = "0x3333333333333333333333333333333333333333", amount = TRADE_USD_WEI } },
            };

        private static CreditsQuoteResult QuoteFor(ShopListingDto listing) =>
            listing.IsStoreMint()
                ? CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForMint(new StoreMintTarget(listing.contractAddress, listing.itemId!, MINT_MANA_WEI), MINT_PRICE_CENTS, MINT_PRICE_CREDITS, BigInteger.Parse(MINT_MANA_WEI)))
                : CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForTrade(CreateTrade(), TRADE_PRICE_CENTS, TRADE_PRICE_CREDITS, BigInteger.Zero, false));

        private static EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError> Authorized(IReadOnlyList<CheckoutLine> lines, string maxCreditedValue = GROUP_CAP_WEI)
        {
            var cents = 0;

            foreach (CheckoutLine line in lines)
                cents += line.UsdPriceCents;

            string creditId = lines.Count == 1 ? CREDIT_TRADE : CREDIT_STORE;

            return EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError>.SuccessResult(new AuthorizeGroupResponse
            {
                credit = new AuthorizedCredit
                {
                    id = creditId,
                    amount = GROUP_CAP_WEI,
                    availableAmount = GROUP_CAP_WEI,
                    expiresAt = 1767225600,
                    signature = "0x" + new string('b', 130),
                    contract = "0x8052a560e6e6ac86eeb7e711a4497f639b322fb3",
                },
                maxCreditedValue = maxCreditedValue,
                usdCents = cents,
                oracleRate = MANA_USD_RATE.ToString(),
                lines = Array.Empty<AuthorizedGroupLine>(),
            });
        }

        private async UniTask<CartReview> ReviewAsync()
        {
            CartReviewResult review = await service.ReviewAsync(cart.Lines, CancellationToken.None);
            Assert.IsTrue(review.Success, $"Review failed with {review.Error}: {review.Message}");
            return review.Review!;
        }

        [Test]
        public async Task ReserveOnceAndSignOncePerTransactionGroup()
        {
            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: the trade is one group, the three mint units another; each group is one credit and one signature.
            Assert.AreEqual(4, review.UnitCount);
            Assert.AreEqual(2, review.GroupCount);
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(3, result.BoughtLineIds.Count);
            Assert.AreEqual(4, result.BoughtUnits.Count);
            Assert.AreEqual(0, cart.Count);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 1 && l[0].TradeId == TRADE_ID), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 3 && l[0].TradeId == null && l[0].ContractAddress == MINT_COLLECTION_A), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), new BigInteger(6), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectASecondCheckoutWithoutTouchingTheRunningOne()
        {
            // Arrange: the first checkout is held while its first group settles.
            var settlementGate = new UniTaskCompletionSource<SettlementOutcome>();

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(settlementGate.Task);

            CartReview review = await ReviewAsync();
            UniTask<CartCheckoutResult> running = service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);
            var completions = 0;
            service.CheckoutCompleted += _ => completions++;

            // Act
            CartCheckoutResult rejected = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: the second call failed on its own and the first is still settling.
            Assert.AreEqual(CartCheckoutOutcome.Failed, rejected.Outcome);
            Assert.IsTrue(service.IsCheckoutInFlight);
            Assert.IsNull(service.LastResult);
            Assert.AreEqual(CartCheckoutStage.WaitingSettlement, service.CurrentProgress.Stage);
            Assert.AreEqual(0, completions);

            settlementGate.TrySetResult(SettlementOutcome.Confirmed);
            CartCheckoutResult result = await running;

            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(1, completions);
        }

        [Test]
        public async Task RefuseACheckoutWhileAnotherRelayedPurchaseIsRunning()
        {
            // Arrange: a single-item purchase or NAME claim holds the gate.
            using RelayedPurchaseGate.Lease? held = purchaseGate.TryEnter();
            CartReview review = await ReviewAsync();

            // Act
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: nothing was reserved or signed, and the cart is untouched.
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.PurchaseInProgress, result.FirstError);
            Assert.IsFalse(service.IsCheckoutInFlight);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HoldTheGateUntilTheCheckoutEnds()
        {
            // Arrange: the checkout is held while its first group settles.
            var settlementGate = new UniTaskCompletionSource<SettlementOutcome>();

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(settlementGate.Task);

            CartReview review = await ReviewAsync();
            UniTask<CartCheckoutResult> running = service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert
            Assert.IsNull(purchaseGate.TryEnter());

            settlementGate.TrySetResult(SettlementOutcome.Confirmed);
            await running;

            Assert.IsNotNull(purchaseGate.TryEnter());
        }

        [Test]
        public async Task KeepTheUnitsAddedToALineAfterItWasReviewed()
        {
            // Arrange: one more unit of an already reviewed mint is added before the checkout settles.
            CartReview review = await ReviewAsync();
            Assert.IsTrue(cart.Add(MintListing(MINT_COLLECTION_A, "1"), ShopCartSource.Grid));

            // Act
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: the four reviewed units were bought and the late one is still in the cart.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(4, result.BoughtUnits.Count);
            Assert.AreEqual(1, cart.Count);
            Assert.AreEqual(1, cart.TotalUnits);
            Assert.IsTrue(cart.Contains(MintListing(MINT_COLLECTION_A, "1")));
        }

        [Test]
        public async Task KeepTheSettledGroupAndReleaseOnlyTheRejectedOne()
        {
            // Arrange: the second signature (the store group, floored at nonce 6) is refused by the buyer.
            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign >= 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected, message: "rejected")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: the trade line left the cart, the mint lines stayed, and only the store credit was released.
            Assert.AreEqual(CartCheckoutOutcome.PartiallyCompleted, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.SignatureRejected, result.FirstError);
            Assert.AreEqual(1, result.BoughtLineIds.Count);
            Assert.AreEqual(3, result.UnboughtUnits.Count);
            Assert.AreEqual(2, cart.Count);
            Assert.IsFalse(cart.Contains(TradeListing()));
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_STORE), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseEveryCreditWhenThePendingConfirmationIsAbandoned()
        {
            // Arrange: the first signature stays pending until its token is cancelled.
            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(call =>
                          {
                              CancellationToken signCt = call.ArgAt<CancellationToken>(4);
                              var pending = new UniTaskCompletionSource<RelayResult>();
                              signCt.Register(() => pending.TrySetCanceled(signCt));
                              return pending.Task;
                          });

            CartReview review = await ReviewAsync();
            UniTask<CartCheckoutResult> running = service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);
            Assert.AreEqual(CartCheckoutStage.Signing, service.CurrentProgress.Stage);

            // Act
            service.CancelPendingConfirmation();
            CartCheckoutResult result = await running;

            // Assert: nothing was bought, the cart is intact and both reserved credits went back.
            Assert.AreEqual(CartCheckoutOutcome.Cancelled, result.Outcome);
            Assert.AreEqual(0, result.BoughtLineIds.Count);
            Assert.AreEqual(3, cart.Count);
            Assert.IsFalse(service.IsCheckoutInFlight);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_TRADE), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_STORE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign >= 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseEverythingReservedAndSignNothingWhenCreditsRunOut()
        {
            // Arrange: the trade group reserves, then the store group is refused for lack of credits.
            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 3), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError>.ErrorResult(
                                CreditsAuthorizeError.InsufficientCredits, "{\"error\":\"Insufficient credits\",\"balanceCents\":100,\"requiredCents\":390}")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.InsufficientCredits, result.Outcome);
            Assert.AreEqual(29, result.MissingCredits);
            Assert.AreEqual(0, result.BoughtLineIds.Count);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_TRADE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign >= 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FinishEverySignedGroupWhenTheBuyerClosesDuringSettlement()
        {
            // Arrange: the buyer closes the modal while the first group is settling.
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(_ =>
                             {
                                 uiCts.Cancel();
                                 return UniTask.FromResult(SettlementOutcome.Confirmed);
                             });

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), uiCts.Token);

            // Assert: both reserved groups were signed and settled, and nothing was released.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(0, cart.Count);
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), BUYER, Arg.Any<string>(), new BigInteger(6), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseEverythingReservedWhenTheAuthorizationIsMalformed()
        {
            // Arrange: the trade group reserves, then the store group comes back with a cap that is not a number.
            LogAssert.Expect(LogType.Exception, new Regex("FormatException"));

            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 3), Arg.Any<CancellationToken>())
                            .Returns(call => UniTask.FromResult(Authorized(call.Arg<IReadOnlyList<CheckoutLine>>(), "not-a-number")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: both credits were reserved by then, and both are released before anything is signed.
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.EncodingFailed, result.FirstError);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 2 && salts[0] == CREDIT_TRADE && salts[1] == CREDIT_STORE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseTheCreditWhenTheBuyerClosesDuringAuthorization()
        {
            // Arrange: the buyer closes the modal while the first group is being authorized; the response still arrives.
            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>())
                            .Returns(call =>
                             {
                                 uiCts.Cancel();
                                 return UniTask.FromResult(Authorized(call.Arg<IReadOnlyList<CheckoutLine>>()));
                             });

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), uiCts.Token);

            // Assert: the request did not run on the buyer's token, so its credit is known and released.
            Assert.AreEqual(CartCheckoutOutcome.Cancelled, result.Outcome);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Is<CancellationToken>(token => token != uiCts.Token));
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_TRADE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepTheSettledGroupWhenALaterGroupThrows()
        {
            // Arrange: the trade group settles, then the store group fails in a way nobody planned for.
            LogAssert.Expect(LogType.Exception, "Exception: boom");

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign >= 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(_ => throw new Exception("boom"));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert: the settled trade is reported and leaves the cart; the credit of the unknown group is kept.
            Assert.AreEqual(CartCheckoutOutcome.PartiallyCompleted, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.UnknownError, result.FirstError);
            Assert.AreEqual(1, result.BoughtLineIds.Count);
            Assert.AreEqual(2, cart.Count);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DropUnavailableAndOwnLinesFromTheReview()
        {
            // Arrange: the trade is the buyer's own listing and mint B is sold out.
            quoteService.QuoteAsync(Arg.Is<ShopListingDto>(l => l.tradeId == TRADE_ID), Arg.Any<CancellationToken>())
                        .Returns(UniTask.FromResult(new CreditsQuoteResult(CreditsPurchaseError.OwnListing)));

            shopAPIClient.GetShopListingForItemAsync(MINT_COLLECTION_B, "2", Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing(MINT_COLLECTION_B, "2", available: 0)));

            // Act
            CartReview review = await ReviewAsync();

            // Assert
            Assert.AreEqual(1, review.Buyable.Count);
            Assert.AreEqual(2, review.Buyable[0].Quantity);
            Assert.AreEqual(2, review.Dropped.Count);
            Assert.IsTrue(review.OrderChanged);
            Assert.AreEqual(1, review.GroupCount);
            Assert.AreEqual(2 * MINT_PRICE_CREDITS, review.LiveTotalCredits);
        }

        private void ListTheTradeOnTheMarketplace() =>
            quoteService.QuoteAsync(Arg.Is<ShopListingDto>(listing => listing.tradeId == TRADE_ID), Arg.Any<CancellationToken>())
                        .Returns(_ =>
                         {
                             TradeDto trade = CreateTrade();
                             trade.contract = chainConfig.OffChainMarketplaceAddress;
                             return UniTask.FromResult(CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForTrade(trade, TRADE_PRICE_CENTS, TRADE_PRICE_CREDITS, BigInteger.Zero, false)));
                         });

        // Half a MANA at $0.25 per MANA is $0.125, charged as 2 whole credits.
        private void PriceTheSecondCollectionBelowTheRelayMinimum() =>
            quoteService.QuoteAsync(Arg.Is<ShopListingDto>(listing => listing.contractAddress == MINT_COLLECTION_B), Arg.Any<CancellationToken>())
                        .Returns(_ => UniTask.FromResult(CreditsQuoteResult.Ok(
                             CreditsPurchaseQuote.ForMint(new StoreMintTarget(MINT_COLLECTION_B, "2", HALF_MANA_WEI), 20, 2, BigInteger.Parse(HALF_MANA_WEI)))));

        private void StubMana(BigInteger balance, BigInteger allowance)
        {
            manaTokenReader.BalanceOfAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(balance));

            manaTokenReader.AllowanceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                           .Returns(UniTask.FromResult(allowance));
        }

        private void StubEveryRelayAsBroadcast() =>
            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH_1, nonce: new BigInteger(5))));

        [Test]
        public async Task SettleEveryGroupDirectlyWhenPayingWithMana()
        {
            // Arrange: the trade draws 10 MANA and the three mints 15.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, HUNDRED_MANA);
            StubEveryRelayAsBroadcast();

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 25 * ONE_MANA), CancellationToken.None);

            // Assert: no credit is reserved, and each group goes to its own contract.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(0, cart.Count);
            Assert.AreEqual(PaymentMethod.Mana, result.Groups[0].Method);
            Assert.AreEqual(PaymentMethod.Mana, result.Groups[1].Method);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.OffChainMarketplaceAddress),
                BUYER, Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.CollectionStoreAddress && contract.IsClassic),
                BUYER, Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAManaCheckoutThroughAnUnknownMarketplace()
        {
            // Arrange: the fixture trade names a contract that is not a marketplace deployment.
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 25 * ONE_MANA), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, result.FirstError);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SpendTheCreditsOnTheFirstGroupsAndCoverTheRestInMana()
        {
            // Arrange: 30 credits pay the $2.50 trade whole and $0.50 of the $3.90 the three mints cost.
            StubMana(HUNDRED_MANA, HUNDRED_MANA);
            StubEveryRelayAsBroadcast();

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 300, credits = 30 } }));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.CreditsAndMana, 300, 30, 14 * ONE_MANA), CancellationToken.None);

            // Assert: only the units that take credits are authorized, and both groups settle through the CreditsManager.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(PaymentMethod.Credits, result.Groups[0].Method);
            Assert.AreEqual(PaymentMethod.CreditsAndMana, result.Groups[1].Method);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 1 && l[0].UsdPriceCents == TRADE_PRICE_CENTS), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 1 && l[0].UsdPriceCents == 50), Arg.Any<CancellationToken>());
            await manaTokenReader.Received(1).AllowanceAsync(BUYER, chainConfig.CreditsManagerAddress, Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(2).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.CreditsManagerAddress),
                BUYER, Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseEveryCreditWhenTheManaLegCannotBePaid()
        {
            // Arrange: the same split, but the buyer holds 1 MANA against a gap of about 13.
            StubMana(ONE_MANA, HUNDRED_MANA);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 300, credits = 30 } }));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.CreditsAndMana, 300, 30, 14 * ONE_MANA), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.InsufficientMana, result.FirstError);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 2), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAManaCheckoutWhenAnyTradeOfAGroupAsksForMoreThanItsPrice()
        {
            // Arrange: a second trade on the same marketplace asks for an NFT next to its price.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, HUNDRED_MANA);

            ShopListingDto greedyListing = TradeListing();
            greedyListing.tradeId = GREEDY_TRADE_ID;
            greedyListing.itemId = "4";
            cart.Add(greedyListing, ShopCartSource.Grid);

            quoteService.QuoteAsync(Arg.Is<ShopListingDto>(listing => listing.tradeId == GREEDY_TRADE_ID), Arg.Any<CancellationToken>())
                        .Returns(_ =>
                         {
                             TradeDto trade = CreateTrade();
                             trade.id = GREEDY_TRADE_ID;
                             trade.contract = chainConfig.OffChainMarketplaceAddress;
                             trade.received = new[] { trade.received[0], new TradeAssetDto { assetType = CreditsTradeEncoder.ASSET_TYPE_ERC721, contractAddress = TRADE_COLLECTION, tokenId = "7" } };
                             return UniTask.FromResult(CreditsQuoteResult.Ok(CreditsPurchaseQuote.ForTrade(trade, TRADE_PRICE_CENTS, TRADE_PRICE_CREDITS, BigInteger.Zero, false)));
                         });

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 35 * ONE_MANA), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, result.FirstError);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task SpendOnlyTheCreditsTheBuyerConfirmed()
        {
            // Arrange: the balance grew to cover the whole cart after the buyer confirmed 30 credits.
            StubMana(HUNDRED_MANA, HUNDRED_MANA);
            StubEveryRelayAsBroadcast();

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 6400, credits = 640 } }));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.CreditsAndMana, 300, 30, 14 * ONE_MANA), CancellationToken.None);

            // Assert: the split is the confirmed one, $2.50 on the trade and $0.50 on the mints.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(PaymentMethod.Credits, result.Groups[0].Method);
            Assert.AreEqual(PaymentMethod.CreditsAndMana, result.Groups[1].Method);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 1 && l[0].UsdPriceCents == 50), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAMixedCheckoutWhoseCreditsDoNotSplitTheBasket()
        {
            // Arrange
            StubMana(HUNDRED_MANA, HUNDRED_MANA);

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.CreditsAndMana, 0, 0, 25 * ONE_MANA), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.FirstError);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ApproveEachSpenderOnOneNonceSequenceBeforePayingWithMana()
        {
            // Arrange: neither the marketplace nor the store may pull the buyer's MANA yet.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 25 * ONE_MANA), CancellationToken.None);

            // Assert: the second approval is floored past the first one's nonce, and each purchase starts its own contract's sequence.
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            await manaTokenReader.Received(1).AllowanceAsync(BUYER, chainConfig.OffChainMarketplaceAddress, Arg.Any<CancellationToken>());
            await manaTokenReader.Received(1).AllowanceAsync(BUYER, chainConfig.CollectionStoreAddress, Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.ManaTokenContract.Address),
                BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.ManaTokenContract.Address),
                BUYER, Arg.Any<string>(), new BigInteger(6), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.OffChainMarketplaceAddress),
                BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());

            await metaTxRelayer.Received(1).RelayAsync(Arg.Is<MetaTxContract>(contract => contract.Address == chainConfig.CollectionStoreAddress),
                BUYER, Arg.Any<string>(), Arg.Is<BigInteger>(n => n.Sign < 0), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAManaCheckoutThatGrewPastWhatTheBuyerConfirmed()
        {
            // Arrange: the buyer confirmed 20 MANA, the cart draws 25.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, HUNDRED_MANA);

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 20 * ONE_MANA), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.FirstError);
            Assert.AreEqual(3, cart.Count);
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task EndAsCancelledWhenTheBuyerDeclinesAManaApproval()
        {
            // Arrange
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            metaTxRelayer.RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected, message: "rejected")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, new PaymentOption(PaymentMethod.Mana, 0, 0, 25 * ONE_MANA), CancellationToken.None);

            // Assert: the first approval was the only thing asked of the buyer.
            Assert.AreEqual(CartCheckoutOutcome.Cancelled, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.SignatureRejected, result.FirstError);
            Assert.AreEqual(3, cart.Count);
            await metaTxRelayer.Received(1).RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task PriceTheWholeBasketInManaForItsPaymentOptions()
        {
            // Arrange: no credits; the $2.50 trade is worth 10 MANA at the rate and the three mints cost 15.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 0, credits = 0 } }));

            CartReview review = await ReviewAsync();

            // Act
            PaymentOptions options = await service.GetPaymentOptionsAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.Mana, options.Options[0].Method);
            Assert.AreEqual(25 * ONE_MANA, options.Options[0].ManaWei);
        }

        [Test]
        public async Task OfferNoManaAloneWhenOneUnitIsBelowTheRelayMinimum()
        {
            // Arrange: no credits and plenty of MANA, but one mint costs half a MANA.
            ListTheTradeOnTheMarketplace();
            PriceTheSecondCollectionBelowTheRelayMinimum();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 0, credits = 0 } }));

            CartReview review = await ReviewAsync();

            // Act
            PaymentOptions options = await service.GetPaymentOptionsAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.IsTrue(options.IsManaAloneRefused);
        }

        [Test]
        public async Task DropTheMixedMethodWhenAGroupWithoutCreditsHoldsAUnitBelowTheRelayMinimum()
        {
            // Arrange: 10 credits are all taken by the trade, so the mints, the cheap one included, would be paid with MANA alone.
            ListTheTradeOnTheMarketplace();
            PriceTheSecondCollectionBelowTheRelayMinimum();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 100, credits = 10 } }));

            CartReview review = await ReviewAsync();

            // Act
            PaymentOptions options = await service.GetPaymentOptionsAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
        }

        [Test]
        public async Task KeepTheMixedMethodWhenCreditsReachTheGroupHoldingTheUnitBelowTheRelayMinimum()
        {
            // Arrange: 30 credits cover the trade and leave 5 for the mints, so both groups go through the CreditsManager.
            ListTheTradeOnTheMarketplace();
            PriceTheSecondCollectionBelowTheRelayMinimum();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 300, credits = 30 } }));

            CartReview review = await ReviewAsync();

            // Act
            PaymentOptions options = await service.GetPaymentOptionsAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, options.Options.Count);
            Assert.AreEqual(PaymentMethod.CreditsAndMana, options.Options[0].Method);
            Assert.AreEqual(300, options.Options[0].CreditsCents);
        }

        [Test]
        public async Task OfferNoManaMethodWhenOneLineHasNoManaPrice()
        {
            // Arrange: the trade's MANA price cannot be resolved, although the mints carry theirs.
            ListTheTradeOnTheMarketplace();
            StubMana(HUNDRED_MANA, BigInteger.Zero);

            creditsAPIClient.GetUserCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(new UserCreditsResponse { usd = new UsdCredits { balanceCents = 100, credits = 10 } }));

            CartReview review = await ReviewAsync();

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(_ => UniTask.FromException<ManaUsdRate>(new InvalidOperationException("rate unavailable")));

            // Act
            PaymentOptions options = await service.GetPaymentOptionsAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, options.Options.Count);
            Assert.IsNull(options.ManaShortfall);
        }

        [Test]
        public async Task RefuseACreditsCheckoutWhoseCreditDoesNotCoverItsCap()
        {
            // Arrange: the cap is twice the credit, and the difference would be pulled from the buyer's MANA.
            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Any<IReadOnlyList<CheckoutLine>>(), Arg.Any<CancellationToken>())
                            .Returns(call => UniTask.FromResult(Authorized(call.Arg<IReadOnlyList<CheckoutLine>>(), "200000000000000000000")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CartReview.CreditsOnly(review), CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.Failed, result.Outcome);
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.FirstError);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_TRADE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayAsync(Arg.Any<MetaTxContract>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }
    }
}
