using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class CreditsPurchaseServiceShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string SELLER = "0x24e5f44999c151f08609f8e27b2238c773c4d020";
        private const string TRADE_ID = "trade-1";
        private const string CREDIT_ID = "intent-1";
        private const string TX_HASH = "0x1122";
        private const string COLLECTION = "0x2222222222222222222222222222222222222222";
        private const string MINT_ITEM_ID = "3";

        // A mint priced at 5 MANA — $1.25 at the fixture rate, charged as 13 whole credits, same arithmetic as a
        // legacy MANA trade because a mint is MANA-denominated too.
        private const string MINT_MANA_WEI = "5000000000000000000";

        // $0.25 per MANA on a Chainlink-style 8-decimal feed.
        private const int ORACLE_DECIMALS = 8;
        private const int MANA_USD_RATE = 25_000_000;

        // The native fixture is USD-pegged at $2.50, so the rate only decides how much MANA it draws at
        // purchase time: 10 MANA.
        private const int NATIVE_PRICE_CENTS = 250;
        private const int NATIVE_PRICE_CREDITS = 25;

        // The legacy fixture is denominated in MANA: 5 MANA is $1.25 at the fixture rate, which the credits-server
        // charges as 13 whole credits.
        private const string LEGACY_MANA_WEI = "5000000000000000000";
        private const int LEGACY_PRICE_CENTS = 130;
        private const int LEGACY_PRICE_CREDITS = 13;

        private MarketplaceShopAPIClient shopAPIClient = null!;
        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private CreditsManagerMetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
        private CreditsChainConfig chainConfig = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CreditsFeatureAccess creditsFeatureAccess = null!;
        private CancellationTokenSource warmUpCts = null!;
        private CreditsPurchaseService service = null!;
        private List<CreditsPurchaseState> recordedStates = null!;

        [SetUp]
        public void SetUp()
        {
            shopAPIClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            metaTxRelayer = Substitute.For<CreditsManagerMetaTxRelayer>(null, null, null, null);
            settlementPoller = Substitute.For<PolygonSettlementPoller>(null, null);
            manaUsdRateReader = Substitute.For<ManaUsdRateReader>(null, null);
            chainConfig = new CreditsChainConfig(DecentralandEnvironment.Zone);
            identityCache = Substitute.For<IWeb3IdentityCache>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<TradeDto?>(CreateTrade()));

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromResult(new ManaUsdRate(MANA_USD_RATE, ORACLE_DECIMALS)));

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization()));

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH)));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            // The wallets restriction flag is absent by default, so every wallet is allowed.
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

            warmUpCts = new CancellationTokenSource();
            creditsFeatureAccess = new CreditsFeatureAccess(identityCache, warmUpCts.Token);

            service = CreateService(isFeatureEnabled: true);

            recordedStates = new List<CreditsPurchaseState>();
            service.StateChanged += state => recordedStates.Add(state);
        }

        [TearDown]
        public void TearDown()
        {
            warmUpCts.Cancel();
            warmUpCts.Dispose();

            FeatureFlagsConfiguration.Reset();
        }

        private CreditsPurchaseService CreateService(bool isFeatureEnabled, CreditsFeatureAccess? featureAccess = null) =>
            new (shopAPIClient, creditsAPIClient, metaTxRelayer, settlementPoller, manaUsdRateReader, chainConfig, identityCache,
                featureAccess ?? creditsFeatureAccess, isFeatureEnabled);

        /// <summary>The listing row a trade is quoted from — the quote resolves the trade through its id.</summary>
        private static ShopListingDto TradeListing() =>
            new ()
            {
                tradeId = TRADE_ID,
                acquisition = "trade",
                listingType = "primary",
                source = "native",
                contractAddress = COLLECTION,
                itemId = MINT_ITEM_ID,
                available = 5,
            };

        /// <summary>
        ///     A CollectionStore mint: no tradeId, no order, priced in MANA, with stock. Everything the quote
        ///     needs is here, which is the point — there is nothing to fetch.
        /// </summary>
        private static ShopListingDto MintListing(int available = 8, string? manaWei = MINT_MANA_WEI) =>
            new ()
            {
                tradeId = null!,
                acquisition = "store",
                listingType = "primary",
                source = "legacy",
                contractAddress = COLLECTION,
                itemId = MINT_ITEM_ID,
                manaWei = manaWei,
                available = available,
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
                checks = new TradeChecksDto
                {
                    uses = 1,
                    expiration = 1893456000000,
                    effective = 1735689600000,
                    salt = "0x1234",
                    allowedRoot = "0x",
                },
                sent = new[]
                {
                    new TradeAssetDto
                    {
                        assetType = CreditsTradeEncoder.ASSET_TYPE_COLLECTION_ITEM,
                        contractAddress = "0x2222222222222222222222222222222222222222",
                        itemId = "3",
                    },
                },
                received = new[]
                {
                    new TradeAssetDto
                    {
                        assetType = CreditsTradeEncoder.ASSET_TYPE_USD_PEGGED_MANA,
                        contractAddress = "0x3333333333333333333333333333333333333333",
                        amount = "2500000000000000000", // exactly 250 cents
                    },
                },
            };

        /// <summary>
        ///     A legacy listing: the trade is denominated in MANA, not USD, so its price in credits exists only
        ///     through the oracle rate.
        /// </summary>
        private static TradeDto CreateLegacyManaTrade()
        {
            TradeDto trade = CreateTrade();
            trade.received[0].assetType = CreditsTradeEncoder.ASSET_TYPE_ERC20;
            trade.received[0].amount = LEGACY_MANA_WEI;
            return trade;
        }

        /// <summary>
        ///     What the credits-server signs for usdCents: a MANA cap sized at the fixture rate plus its 2% buffer,
        ///     which is all the CreditsManager can put behind the trade. manaCapWei overrides that sizing, to stand
        ///     in for a server that read a different rate than the quote did.
        /// </summary>
        private static AuthorizeCreditResponse CreateAuthorization(int usdCents = NATIVE_PRICE_CENTS, string? manaCapWei = null)
        {
            string cap = manaCapWei ?? (new BigInteger(usdCents) * BigInteger.Pow(10, 16) * BigInteger.Pow(10, ORACLE_DECIMALS) / MANA_USD_RATE * 10200 / 10000).ToString();

            return new AuthorizeCreditResponse
            {
                credit = new AuthorizedCredit
                {
                    id = CREDIT_ID,
                    amount = cap,
                    availableAmount = cap,
                    expiresAt = 1767225600,
                    signature = "0x" + new string('b', 130),
                    contract = "0x8052a560e6e6ac86eeb7e711a4497f639b322fb3",
                },
                maxCreditedValue = cap,
                usdCents = usdCents,
                oracleRate = MANA_USD_RATE.ToString(),
            };
        }

        private async UniTask<CreditsPurchaseResult> QuoteAndPurchaseAsync()
        {
            CreditsQuoteResult quote = await service.QuoteAsync(TradeListing(), CancellationToken.None);
            Assert.IsTrue(quote.Success, $"Quote failed with {quote.Error}: {quote.Message}");
            return await service.PurchaseAsync(quote.Quote, CancellationToken.None);
        }

        [Test]
        public async Task CompletePurchaseThroughGaslessPath()
        {
            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(TX_HASH, result.TxHash);
            CollectionAssert.AreEqual(
                new[] { CreditsPurchaseState.ResolvingListing, CreditsPurchaseState.ResolvingListing, CreditsPurchaseState.Authorizing, CreditsPurchaseState.Signing, CreditsPurchaseState.WaitingSettlement, CreditsPurchaseState.Success },
                recordedStates);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task QuoteNativeTradeFromItsUsdPeggedAmountWithoutTheOracle()
        {
            // Arrange: a dead oracle must not stop a USD-pegged quote — its price needs no rate. Thrown lazily
            // so the never-consumed exception task cannot surface as an unobserved UniTask error.
            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns<UniTask<ManaUsdRate>>(_ => throw new InvalidOperationException("stale"));

            // Act
            CreditsQuoteResult result = await service.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert: the settlement wei is left for purchase time, and only the cache warm-up touches the reader.
            Assert.IsTrue(result.Success);
            Assert.AreEqual(NATIVE_PRICE_CENTS, result.Quote.UsdCents);
            Assert.AreEqual(NATIVE_PRICE_CREDITS, result.Quote.Credits);
            Assert.AreEqual(BigInteger.Zero, result.Quote.RequiredManaWei);
            Assert.IsFalse(result.Quote.IsLiveRatePrice);
            await manaUsdRateReader.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await manaUsdRateReader.Received(1).PrefetchAsync(Arg.Any<string>());
        }

        [Test]
        public async Task QuoteLegacyManaTradeAtTheLiveOracleRate()
        {
            // Arrange
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(CreateLegacyManaTrade()));

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(LEGACY_PRICE_CENTS)));

            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(TradeListing(), CancellationToken.None);
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert: the MANA price is converted at the rate settlement uses, then rounded up to a whole credit.
            Assert.IsTrue(quote.Success);
            Assert.AreEqual(LEGACY_PRICE_CENTS, quote.Quote.UsdCents);
            Assert.AreEqual(LEGACY_PRICE_CREDITS, quote.Quote.Credits);
            Assert.AreEqual(BigInteger.Parse(LEGACY_MANA_WEI), quote.Quote.RequiredManaWei);
            Assert.IsTrue(quote.Quote.IsLiveRatePrice);
            Assert.IsTrue(result.Success);
            await creditsAPIClient.Received(1).AuthorizeUsdCreditAsync(LEGACY_PRICE_CENTS, TRADE_ID, string.Empty, string.Empty, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectLegacyQuoteWhenTheOracleRateIsUnavailable()
        {
            // Arrange: a MANA-denominated price cannot be displayed without the rate.
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(CreateLegacyManaTrade()));

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromException<ManaUsdRate>(new InvalidOperationException("stale")));

            // Act
            CreditsQuoteResult result = await service.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceUnavailable, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectPurchaseWithoutChargingWhenTheRateIsUnavailableAtConfirm()
        {
            // Arrange: the USD-pegged quote succeeds, then the oracle dies before the confirm click.
            CreditsQuoteResult quote = await service.QuoteAsync(TradeListing(), CancellationToken.None);
            Assert.IsTrue(quote.Success);

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromException<ManaUsdRate>(new InvalidOperationException("stale")));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert: the rate failed before any credit intent existed, so there is nothing to release.
            Assert.AreEqual(CreditsPurchaseError.PriceUnavailable, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenThePurchaseTimeRateCannotCoverTheTrade()
        {
            // Arrange: MANA halves between authorization sizing and the purchase-time read, so the trade draws
            // 20 MANA against a cap sized for ~10.
            CreditsQuoteResult quote = await service.QuoteAsync(TradeListing(), CancellationToken.None);
            Assert.IsTrue(quote.Success);

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromResult(new ManaUsdRate(MANA_USD_RATE / 2, ORACLE_DECIMALS)));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWhenFeatureIsDisabled()
        {
            // Arrange
            CreditsPurchaseService disabledService = CreateService(isFeatureEnabled: false);

            // Act
            CreditsQuoteResult result = await disabledService.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.FeatureDisabled, result.Error);
            await shopAPIClient.DidNotReceive().GetTradeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWhenWalletIsOutsideTheAllowlist()
        {
            // Arrange: the wallets restriction flag is on and the buyer is not listed
            FeatureFlagsResultDto dto = FeatureFlagsResultDto.Empty;
            dto.flags[FeatureFlagsStrings.CREDITS_WALLETS] = true;

            dto.variants[FeatureFlagsStrings.CREDITS_WALLETS] = new FeatureFlagVariantDto
            {
                name = FeatureFlagsStrings.WALLETS_VARIANT,
                enabled = true,
                payload = new FeatureFlagPayload { type = "string", value = SELLER },
            };

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(dto));

            CreditsPurchaseService restrictedService = CreateService(isFeatureEnabled: true, new CreditsFeatureAccess(identityCache, warmUpCts.Token));

            // Act
            CreditsQuoteResult result = await restrictedService.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.FeatureDisabled, result.Error);
            await shopAPIClient.DidNotReceive().GetTradeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectBuyingOwnListingBeforeCharging()
        {
            // Arrange
            TradeDto trade = CreateTrade();
            trade.signer = BUYER;
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(trade));

            // Act
            CreditsQuoteResult result = await service.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.OwnListing, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectTradesPricedInAnAssetCreditsCannotPay()
        {
            // Arrange
            TradeDto trade = CreateTrade();
            trade.received[0].assetType = CreditsTradeEncoder.ASSET_TYPE_ERC721;
            trade.received[0].tokenId = "7";
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(trade));

            // Act
            CreditsQuoteResult result = await service.QuoteAsync(TradeListing(), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task MapAuthorizationFailureWithoutReleasingAnything()
        {
            // Arrange
            LogAssert.Expect(LogType.Exception, "Exception: boom");

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromException<AuthorizeCreditResponse>(new Exception("boom")));

            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(TradeListing(), CancellationToken.None);
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.AuthorizationFailed, result.Error);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenAuthorizationPricesAboveTheConfirmedAmount()
        {
            // Arrange
            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(NATIVE_PRICE_CENTS + 50)));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task CompletePurchaseWhenAuthorizationPricesBelowTheConfirmedAmount()
        {
            // Arrange: a cheaper charge whose cap still covers the trade — the server read a rate kinder than ours.
            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(NATIVE_PRICE_CENTS - 50, manaCapWei: "10200000000000000000")));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.IsTrue(result.Success);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenTheAuthorizedCapCannotCoverTheTrade()
        {
            // Arrange: the credit was sized for 10 cents while the trade draws 5 MANA — the shape of a legacy
            // listing authorized off a stale catalogue price, which reverts on-chain if it is ever submitted.
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(CreateLegacyManaTrade()));

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(10)));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenSignatureIsRejected()
        {
            // Arrange
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected)));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SignatureRejected, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWithoutAskingForASecondSignatureWhenTheRelayerRefuses()
        {
            // Arrange
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.RelayerRejected, message: "down")));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert: the relayer refused what it could not estimate, so the purchase stops at that one signature.
            Assert.AreEqual(CreditsPurchaseError.RelayerUnavailable, result.Error);
            Assert.AreEqual("down", result.Message);
            await metaTxRelayer.Received(1).RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenTransactionReverts()
        {
            // Arrange
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Reverted));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.TransactionReverted, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepReservationWhenSettlementIsPending()
        {
            // Arrange
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Pending));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SettlementPending, result.Error);
            Assert.AreEqual(TX_HASH, result.TxHash);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepReservationWhenBroadcastIsAmbiguous()
        {
            // Arrange
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.AmbiguousBroadcast, message: "timeout")));

            // Act
            CreditsPurchaseResult result = await QuoteAndPurchaseAsync();

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SettlementPending, result.Error);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
            await settlementPoller.DidNotReceive().WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        }

        /// <summary>
        ///     BUYING A COLLECTIONSTORE MINT.
        ///
        ///     A mint has no trade and no order: nothing was ever signed or listed, and the item is minted straight
        ///     from the store contract. Measured in production — the mint at 0xf90645e2…6213 could not be bought at
        ///     all while the trade at 0x58628c49…a212 went through for the same buyer in the same session, because
        ///     the whole quote was derived from a trade that does not exist for a mint.
        /// </summary>
        [Test]
        public async Task QuoteAMintWithoutFetchingATrade()
        {
            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            // Assert
            Assert.IsTrue(quote.Success, $"Quote failed with {quote.Error}: {quote.Message}");
            Assert.AreEqual(CreditsListingKind.StoreMint, quote.Quote.Kind);
            Assert.AreEqual(LEGACY_PRICE_CENTS, quote.Quote.UsdCents); // MANA-priced, so the oracle decides
            Assert.AreEqual(LEGACY_PRICE_CREDITS, quote.Quote.Credits);
            Assert.AreEqual(BigInteger.Parse(MINT_MANA_WEI), quote.Quote.RequiredManaWei);
            Assert.AreEqual(COLLECTION, quote.Quote.Mint.CollectionAddress);
            Assert.AreEqual(MINT_ITEM_ID, quote.Quote.Mint.ItemId);

            // The point: /v1/trades was never called, because there is nothing there to call it with.
            await shopAPIClient.DidNotReceive().GetTradeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseASoldOutMintOnStockRatherThanOnAMissingTrade()
        {
            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(available: 0), CancellationToken.None);

            // Assert — carried to an on-chain revert otherwise.
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, quote.Error);
        }

        [Test]
        public async Task RefuseAMintWithNoPrice()
        {
            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(manaWei: null), CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, quote.Error);
        }

        /// <summary>
        ///     A missing `acquisition` is an OLDER SERVER, and back then every row was a trade. Reading the absence
        ///     as "mint" would route a trade down the store rail and revert.
        /// </summary>
        [Test]
        public async Task TreatAnAbsentAcquisitionAsATrade()
        {
            // Arrange
            ShopListingDto listing = TradeListing();
            listing.acquisition = null;

            // Act
            CreditsQuoteResult quote = await service.QuoteAsync(listing, CancellationToken.None);

            // Assert
            Assert.IsTrue(quote.Success, $"Quote failed with {quote.Error}: {quote.Message}");
            Assert.AreEqual(CreditsListingKind.Trade, quote.Quote.Kind);
            await shopAPIClient.Received(1).GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>());
        }

        /// <summary>
        ///     The mint's price is an ARGUMENT to CollectionStore.buy and the contract re-validates it against the
        ///     item's live price, so it is re-read immediately before encoding. A trade cannot fail this way — its
        ///     price is signed into the order.
        /// </summary>
        [Test]
        public async Task RereadTheMintPriceBeforeBuying()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing()));

            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.None, result.Error);
            await shopAPIClient.Received(1).GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAMintWhosePriceWentUpBeforeBuying()
        {
            // Arrange — the creator raised it between the quote and the click.
            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            shopAPIClient.GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing(manaWei: "9000000000000000000")));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert — never charged silently at the new price.
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RefuseAMintThatSoldOutBetweenTheQuoteAndTheClick()
        {
            // Arrange
            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            shopAPIClient.GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing(available: 0)));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, result.Error);
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        /// <summary>
        ///     A mint is identified to the credits-server by WHAT it is, since it has no trade. Neither identifier
        ///     is part of what the server signs — it signs a voucher for an amount — but without the pair the
        ///     buyer's purchase history has no way to name the item.
        /// </summary>
        [Test]
        public async Task AuthorizeAMintByItsItemRatherThanByATradeId()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing()));

            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            // Act
            await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert
            await creditsAPIClient.Received(1).AuthorizeUsdCreditAsync(
                LEGACY_PRICE_CENTS, string.Empty, COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RelayTheMintSoABuyerWithNoPolCanStillBuyIt()
        {
            // Arrange
            shopAPIClient.GetShopListingForItemAsync(COLLECTION, MINT_ITEM_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<ShopListingDto?>(MintListing()));

            CreditsQuoteResult quote = await service.QuoteAsync(MintListing(), CancellationToken.None);

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(quote.Quote, CancellationToken.None);

            // Assert — the same gasless rail a trade takes: the buyer signs, the relayer pays.
            Assert.AreEqual(CreditsPurchaseError.None, result.Error);
            Assert.AreEqual(TX_HASH, result.TxHash);
            await metaTxRelayer.Received(1).RelayUseCreditsAsync(BUYER, Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
