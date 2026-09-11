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
using System.Threading;
using System.Threading.Tasks;

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

        // Plenty for every group: 100 MANA.
        private const string GROUP_CAP_WEI = "100000000000000000000";

        private MarketplaceShopAPIClient shopAPIClient = null!;
        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private CreditsManagerMetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
        private ICreditsPurchaseService quoteService = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CancellationTokenSource warmUpCts = null!;
        private ShopCart cart = null!;
        private CreditsCartCheckoutService service = null!;

        [SetUp]
        public void SetUp()
        {
            shopAPIClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            metaTxRelayer = Substitute.For<CreditsManagerMetaTxRelayer>(null, null, null, null);
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

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH_1, nonce: new BigInteger(5))));

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH_2, nonce: new BigInteger(6))));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

            warmUpCts = new CancellationTokenSource();

            cart = new ShopCart(identityCache);
            cart.Add(TradeListing(), ShopCartSource.Grid);
            cart.Add(MintListing(MINT_COLLECTION_A, "1"), ShopCartSource.Outfit, "outfit-1");
            cart.Add(MintListing(MINT_COLLECTION_A, "1"), ShopCartSource.Outfit, "outfit-1");
            cart.Add(MintListing(MINT_COLLECTION_B, "2"), ShopCartSource.Grid);

            service = new CreditsCartCheckoutService(cart, quoteService, shopAPIClient, creditsAPIClient, metaTxRelayer, settlementPoller,
                manaUsdRateReader, new CreditsChainConfig(EthereumNetwork.Sepolia), identityCache, new CreditsFeatureAccess(identityCache, warmUpCts.Token), true);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            cart.Dispose();
            warmUpCts.Cancel();
            warmUpCts.Dispose();
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

        private static EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError> Authorized(IReadOnlyList<CheckoutLine> lines)
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
                maxCreditedValue = GROUP_CAP_WEI,
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
            CartCheckoutResult result = await service.CheckoutAsync(review, CancellationToken.None);

            // Assert: the trade is one group, the three mint units another; each group is one credit and one signature.
            Assert.AreEqual(4, review.UnitCount);
            Assert.AreEqual(2, review.GroupCount);
            Assert.AreEqual(CartCheckoutOutcome.Completed, result.Outcome);
            Assert.AreEqual(3, result.BoughtLineIds.Count);
            Assert.AreEqual(4, result.BoughtUnits.Count);
            Assert.AreEqual(0, cart.Count);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 1 && l[0].TradeId == TRADE_ID), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 3 && l[0].TradeId == null && l[0].ContractAddress == MINT_COLLECTION_A), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayUseCreditsAsync(BUYER, Arg.Any<string>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.Received(1).RelayUseCreditsAsync(BUYER, Arg.Any<string>(), new BigInteger(6), Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepTheSettledGroupAndReleaseOnlyTheRejectedOne()
        {
            // Arrange: the second signature (the store group, floored at nonce 6) is refused by the buyer.
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected, message: "rejected")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CancellationToken.None);

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
        public async Task ReleaseEverythingReservedAndSignNothingWhenCreditsRunOut()
        {
            // Arrange: the trade group reserves, then the store group is refused for lack of credits.
            creditsAPIClient.AuthorizeUsdCreditGroupAsync(Arg.Is<IReadOnlyList<CheckoutLine>>(l => l.Count == 3), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(EnumResult<AuthorizeGroupResponse, CreditsAuthorizeError>.ErrorResult(
                                CreditsAuthorizeError.InsufficientCredits, "{\"error\":\"Insufficient credits\",\"balanceCents\":100,\"requiredCents\":390}")));

            // Act
            CartReview review = await ReviewAsync();
            CartCheckoutResult result = await service.CheckoutAsync(review, CancellationToken.None);

            // Assert
            Assert.AreEqual(CartCheckoutOutcome.InsufficientCredits, result.Outcome);
            Assert.AreEqual(29, result.MissingCredits);
            Assert.AreEqual(0, result.BoughtLineIds.Count);
            Assert.AreEqual(3, cart.Count);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_TRADE), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BigInteger>(), Arg.Any<CancellationToken>());
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
    }
}
