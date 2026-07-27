using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
        private const int PRICE_CREDITS = 25; // == 250 cents == 2.5 USD wei price below

        private MarketplaceShopAPIClient shopAPIClient = null!;
        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private CreditsManagerMetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private IWeb3IdentityCache identityCache = null!;
        private ICompositeWeb3Provider web3Provider = null!;
        private CreditsPurchaseService service = null!;
        private List<CreditsPurchaseState> recordedStates = null!;

        [SetUp]
        public void SetUp()
        {
            shopAPIClient = Substitute.For<MarketplaceShopAPIClient>(null, null);
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            metaTxRelayer = Substitute.For<CreditsManagerMetaTxRelayer>(null, null, null, null);
            settlementPoller = Substitute.For<PolygonSettlementPoller>(null, null);
            identityCache = Substitute.For<IWeb3IdentityCache>();
            web3Provider = Substitute.For<ICompositeWeb3Provider>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult<TradeDto?>(CreateTrade()));

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization()));

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH)));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            service = new CreditsPurchaseService(
                shopAPIClient, creditsAPIClient, metaTxRelayer, settlementPoller,
                new CreditsChainConfig(DecentralandEnvironment.Zone), identityCache, web3Provider, isFeatureEnabled: true);

            recordedStates = new List<CreditsPurchaseState>();
            service.StateChanged += state => recordedStates.Add(state);
        }

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

        private static AuthorizeCreditResponse CreateAuthorization() =>
            new ()
            {
                credit = new AuthorizedCredit
                {
                    id = CREDIT_ID,
                    amount = "6000000000000000000",
                    availableAmount = "6000000000000000000",
                    expiresAt = 1767225600,
                    signature = "0x" + new string('b', 130),
                    contract = "0x8052a560e6e6ac86eeb7e711a4497f639b322fb3",
                },
                maxCreditedValue = "7000000000000000000",
                usdCents = 250,
                oracleRate = "1000000000000000000",
            };

        [Test]
        public async Task CompletePurchaseThroughGaslessPath()
        {
            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(TX_HASH, result.TxHash);
            CollectionAssert.AreEqual(
                new[] { CreditsPurchaseState.ResolvingListing, CreditsPurchaseState.Authorizing, CreditsPurchaseState.Signing, CreditsPurchaseState.WaitingSettlement, CreditsPurchaseState.Success },
                recordedStates);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWhenFeatureIsDisabled()
        {
            // Arrange
            var disabledService = new CreditsPurchaseService(
                shopAPIClient, creditsAPIClient, metaTxRelayer, settlementPoller,
                new CreditsChainConfig(DecentralandEnvironment.Zone), identityCache, web3Provider, isFeatureEnabled: false);

            // Act
            CreditsPurchaseResult result = await disabledService.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

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
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.OwnListing, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectPurchaseWhenPriceChangedBeforeCharging()
        {
            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS + 5, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.PriceChanged, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task MapAuthorizationFailureWithoutReleasingAnything()
        {
            // Arrange
            LogAssert.Expect(LogType.Exception, "Exception: boom");

            creditsAPIClient.AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromException<AuthorizeCreditResponse>(new Exception("boom")));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.AuthorizationFailed, result.Error);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenSignatureIsRejected()
        {
            // Arrange
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.SignatureRejected)));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SignatureRejected, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FailWithoutWalletFallbackOnThirdWebWallets()
        {
            // Arrange
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.RelayerRejected, message: "down")));

            web3Provider.IsThirdWebOTP.Returns(true);

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.RelayerUnavailable, result.Error);
            await web3Provider.DidNotReceive().SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FallBackToWalletTransactionOnDappWallets()
        {
            // Arrange
            const string FALLBACK_TX_HASH = "0xfa11bacc";

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.RelayerRejected, message: "down")));

            web3Provider.IsThirdWebOTP.Returns(false);

            web3Provider.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                        .Returns(UniTask.FromResult(new EthApiResponse { result = FALLBACK_TX_HASH }));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(FALLBACK_TX_HASH, result.TxHash);
            await web3Provider.Received(1).SendAsync(Arg.Is<EthApiRequest>(r => r.method == "eth_sendTransaction"), Web3RequestSource.Internal, Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseReservationWhenTransactionReverts()
        {
            // Arrange
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Reverted));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

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
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

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
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.SettlementPending, result.Error);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
            await settlementPoller.DidNotReceive().WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task RejectTradesThatAreNotUsdPegged()
        {
            // Arrange
            TradeDto trade = CreateTrade();
            trade.received[0].assetType = CreditsTradeEncoder.ASSET_TYPE_ERC20;
            shopAPIClient.GetTradeAsync(TRADE_ID, Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<TradeDto?>(trade));

            // Act
            CreditsPurchaseResult result = await service.PurchaseAsync(TRADE_ID, PRICE_CREDITS, CancellationToken.None);

            // Assert
            Assert.AreEqual(CreditsPurchaseError.ListingNotAvailable, result.Error);
            await creditsAPIClient.DidNotReceive().AuthorizeUsdCreditAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
