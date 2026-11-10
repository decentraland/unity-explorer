using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits.Purchase.Names;
using DCL.Web3;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class NameClaimServiceShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string NAME = "Carolo";
        private const string CREDIT_ID = "0x00000000000000000000000000000000000000000000000000000000000000aa";
        private const string TX_HASH = "0x1122";
        private const string ROUTE_TARGET = "0xc9a9c416fbfd424d09e3f905142799abb10c2fa7";
        private const string ROUTE_SELECTOR = "0xfd165a73";
        private const string ROUTE_DATA = "0x00000000000000000000000000000000000000000000000000000000deadbeef";
        private const long ROUTE_EXPIRES_AT = 1767225600;
        private const string NAME_PRICE_WEI = "100000000000000000000";
        private const string UNDER_SIZED_WEI = "99000000000000000000";
        private const string AVAILABLE_WORD = "0x0000000000000000000000000000000000000000000000000000000000000001";
        private const string TAKEN_WORD = "0x0000000000000000000000000000000000000000000000000000000000000000";

        // $0.25 per MANA on an 8-decimal feed: 100 MANA is $25, which is 250 credits.
        private const int ORACLE_DECIMALS = 8;
        private const int MANA_USD_RATE = 25_000_000;
        private const int NAME_PRICE_CENTS = 2500;
        private const int NAME_PRICE_CREDITS = 250;

        private static readonly string ROUTE_SALT = "0x" + new string('5', 64);
        private static readonly string ROUTE_SIGNATURE = "0x" + new string('d', 130);

        private MarketplaceCreditsAPIClient creditsAPIClient = null!;
        private IEthereumApi ethereumApi = null!;
        private CreditsManagerMetaTxRelayer metaTxRelayer = null!;
        private PolygonSettlementPoller settlementPoller = null!;
        private ManaUsdRateReader manaUsdRateReader = null!;
        private AcrossBridgeStatusPoller bridgeStatusPoller = null!;
        private CreditsChainConfig chainConfig = null!;
        private IWeb3IdentityCache identityCache = null!;
        private CancellationTokenSource warmUpCts = null!;
        private NameClaimService service = null!;
        private List<NameClaimStage> recordedStages = null!;
        private List<NameClaimResult> completedResults = null!;

        [SetUp]
        public void SetUp()
        {
            creditsAPIClient = Substitute.For<MarketplaceCreditsAPIClient>(null, null);
            ethereumApi = Substitute.For<IEthereumApi>();
            metaTxRelayer = Substitute.For<CreditsManagerMetaTxRelayer>(null, null, null, null);
            settlementPoller = Substitute.For<PolygonSettlementPoller>(null, null);
            manaUsdRateReader = Substitute.For<ManaUsdRateReader>(null, null);
            bridgeStatusPoller = Substitute.For<AcrossBridgeStatusPoller>(null, null);
            chainConfig = new CreditsChainConfig(EthereumNetwork.Mainnet);
            identityCache = Substitute.For<IWeb3IdentityCache>();

            IWeb3Identity identity = Substitute.For<IWeb3Identity>();
            identity.Address.Returns(new Web3Address(BUYER));
            identityCache.Identity.Returns(identity);

            manaUsdRateReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                             .Returns(UniTask.FromResult(new ManaUsdRate(MANA_USD_RATE, ORACLE_DECIMALS)));

            creditsAPIClient.GetNameRouteAsync(NAME, chainConfig.ChainId, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateRoute()));

            creditsAPIClient.AuthorizeUsdNameCreditAsync(Arg.Any<int>(), NAME, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(NAME_PRICE_WEI)));

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH)));

            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Confirmed));

            bridgeStatusPoller.WaitForFillAsync(TX_HASH, Arg.Any<CancellationToken>())
                              .Returns(UniTask.FromResult(AcrossFillOutcome.Registered));

            // The wallets restriction flag is absent by default, so every wallet is allowed.
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

            warmUpCts = new CancellationTokenSource();

            service = new NameClaimService(creditsAPIClient, ethereumApi, metaTxRelayer, settlementPoller, manaUsdRateReader, bridgeStatusPoller,
                chainConfig, identityCache, new CreditsFeatureAccess(identityCache, warmUpCts.Token), true);

            recordedStages = new List<NameClaimStage>();
            completedResults = new List<NameClaimResult>();
            service.StageChanged += stage => recordedStages.Add(stage);
            service.Completed += (_, result) => completedResults.Add(result);
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            warmUpCts.Cancel();
            warmUpCts.Dispose();

            FeatureFlagsConfiguration.Reset();
        }

        [TestCase(AVAILABLE_WORD, ExpectedResult = NameAvailability.Available)]
        [TestCase(TAKEN_WORD, ExpectedResult = NameAvailability.Taken)]
        [TestCase("0x", ExpectedResult = NameAvailability.Unknown)]
        [TestCase("", ExpectedResult = NameAvailability.Unknown)]
        [TestCase(null, ExpectedResult = NameAvailability.Unknown)]
        public NameAvailability DecodeTheRegistrarAnswer(string? resultHex) =>
            NameClaimService.DecodeAvailability(resultHex);

        [TestCase(404, "{\"code\":\"NAMES_DISABLED\"}", ExpectedResult = NameClaimError.FeatureDisabled)]
        [TestCase(400, "{\"error\":\"Name is not available. It may already be registered.\"}", ExpectedResult = NameClaimError.NameUnavailable)]
        [TestCase(400, "{\"error\":\"Invalid chainId: x\"}", ExpectedResult = NameClaimError.RouteUnavailable)]
        [TestCase(503, "{\"code\":\"ROUTE_COST_TOO_HIGH\"}", ExpectedResult = NameClaimError.RouteTooExpensive)]
        [TestCase(503, "", ExpectedResult = NameClaimError.RouteUnavailable)]
        [TestCase(500, null, ExpectedResult = NameClaimError.RouteUnavailable)]
        public NameClaimError MapTheRouteRefusal(long responseCode, string? responseText) =>
            NameClaimService.MapRouteError(responseCode, responseText);

        [TestCase("filled", true, ExpectedResult = AcrossFillOutcome.Registered)]
        [TestCase("FILLED", false, ExpectedResult = AcrossFillOutcome.NotRegistered)]
        [TestCase("filled", null, ExpectedResult = AcrossFillOutcome.Unknown)]
        [TestCase("refunded", null, ExpectedResult = AcrossFillOutcome.NotRegistered)]
        [TestCase("expired", null, ExpectedResult = AcrossFillOutcome.NotRegistered)]
        [TestCase("pending", null, ExpectedResult = null)]
        [TestCase(null, null, ExpectedResult = null)]
        public AcrossFillOutcome? InterpretTheBridgeStatus(string? status, bool? actionsSucceeded) =>
            AcrossBridgeStatusPoller.Interpret(status, actionsSucceeded);

        [Test]
        public async Task ReadAvailabilityFromTheRegistrarOnEthereum()
        {
            // Arrange
            EthApiRequest sent = default(EthApiRequest);

            ethereumApi.SendAsync(Arg.Do<EthApiRequest>(request => sent = request), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(UniTask.FromResult(new EthApiResponse { result = TAKEN_WORD }));

            // Act
            NameAvailability availability = await service.CheckAvailabilityAsync(NAME, CancellationToken.None);

            // Assert
            Assert.AreEqual(NameAvailability.Taken, availability);
            Assert.AreEqual(chainConfig.EthereumReadonlyNetwork, sent.readonlyNetwork);
            Assert.AreEqual(chainConfig.DclRegistrarAddress, ((JObject)sent.@params![0])["to"]!.ToString());
        }

        [Test]
        public async Task ReportUnknownAvailabilityWhenTheRegistrarCannotBeRead()
        {
            // Arrange
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(_ => throw new InvalidOperationException("rpc down"));

            // Act
            NameAvailability availability = await service.CheckAvailabilityAsync(NAME, CancellationToken.None);

            // Assert
            Assert.AreEqual(NameAvailability.Unknown, availability);
        }

        [Test]
        public async Task QuoteTheFixedManaPriceInWholeCredits()
        {
            // Act
            NameQuoteResult quote = await service.QuoteAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(quote.Success);
            Assert.AreEqual(NAME_PRICE_CENTS, quote.Quote.UsdCents);
            Assert.AreEqual(NAME_PRICE_CREDITS, quote.Quote.Credits);
        }

        [Test]
        public async Task RegisterTheNameThroughTheServerSignedRoute()
        {
            // Arrange
            var relayedCalldata = string.Empty;

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Do<string>(calldata => relayedCalldata = calldata), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH)));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Registered, result.Status);
            StringAssert.Contains(ROUTE_TARGET.Substring(2), relayedCalldata);
            StringAssert.Contains(ROUTE_SELECTOR.Substring(2), relayedCalldata);
            StringAssert.Contains(ROUTE_SALT.Substring(2), relayedCalldata);
            StringAssert.Contains(ROUTE_SIGNATURE.Substring(2), relayedCalldata);
            CollectionAssert.AreEqual(new[] { NameClaimStage.Preparing, NameClaimStage.Signing, NameClaimStage.Confirming, NameClaimStage.Registering }, recordedStages);
            Assert.AreEqual(1, completedResults.Count);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseTheCreditWhenItCannotCoverTheNamePrice()
        {
            // Arrange
            creditsAPIClient.AuthorizeUsdNameCreditAsync(Arg.Any<int>(), NAME, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization(UNDER_SIZED_WEI)));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Failed, result.Status);
            Assert.AreEqual(NameClaimError.PriceChanged, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepTheCreditWhenTheBridgeDoesNotRegisterTheName()
        {
            // Arrange
            bridgeStatusPoller.WaitForFillAsync(TX_HASH, Arg.Any<CancellationToken>())
                              .Returns(UniTask.FromResult(AcrossFillOutcome.NotRegistered));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Failed, result.Status);
            Assert.AreEqual(NameClaimError.NotRegistered, result.Error);
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReportPendingWhenTheBridgeOutcomeIsUnknown()
        {
            // Arrange
            bridgeStatusPoller.WaitForFillAsync(TX_HASH, Arg.Any<CancellationToken>())
                              .Returns(UniTask.FromResult(AcrossFillOutcome.Unknown));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Pending, result.Status);
        }

        [Test]
        public async Task ReportPendingWithoutFollowingTheBridgeWhenThePaymentDidNotSettle()
        {
            // Arrange
            settlementPoller.WaitForSettlementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(SettlementOutcome.Pending));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Pending, result.Status);
            await bridgeStatusPoller.DidNotReceive().WaitForFillAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await creditsAPIClient.DidNotReceive().ReleaseUsdIntentsAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task FollowTheBridgeWhenTheBuyerClosesAfterSigning()
        {
            // Arrange: the buyer closes the modal as soon as the payment is signed and relayed.
            using var uiCts = new CancellationTokenSource();

            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns(_ =>
                          {
                              uiCts.Cancel();
                              return UniTask.FromResult(new RelayResult(RelayOutcome.Broadcast, TX_HASH));
                          });

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), uiCts.Token);

            // Assert: settlement and the bridge ran on the service lifetime, and the result waits to be read.
            Assert.AreEqual(NameClaimStatus.Registered, result.Status);
            await settlementPoller.Received(1).WaitForSettlementAsync(TX_HASH, Arg.Any<TimeSpan>(), Arg.Is<CancellationToken>(token => token != uiCts.Token));
            await bridgeStatusPoller.Received(1).WaitForFillAsync(TX_HASH, Arg.Any<CancellationToken>());
            Assert.AreEqual(NAME, service.LastResultName);
            Assert.AreEqual(NameClaimStatus.Registered, service.LastResult.Status);
        }

        [Test]
        public async Task ReleaseTheCreditAndCancelWhenTheBuyerClosesBeforeSigning()
        {
            // Arrange: the wallet prompt is cancelled by the modal closing.
            metaTxRelayer.RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>())
                         .Returns<UniTask<RelayResult>>(_ => throw new OperationCanceledException());

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Failed, result.Status);
            Assert.AreEqual(NameClaimError.Cancelled, result.Error);
            Assert.IsNull(service.LastResultName);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await bridgeStatusPoller.DidNotReceive().WaitForFillAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseTheCreditWhenTheAuthorizationIsMalformed()
        {
            // Arrange
            LogAssert.Expect(LogType.Exception, new Regex("FormatException"));

            creditsAPIClient.AuthorizeUsdNameCreditAsync(Arg.Any<int>(), NAME, Arg.Any<CancellationToken>())
                            .Returns(UniTask.FromResult(CreateAuthorization("not-a-number")));

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimStatus.Failed, result.Status);
            Assert.AreEqual(NameClaimError.AuthorizationFailed, result.Error);
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReleaseTheCreditWhenTheBuyerClosesDuringAuthorization()
        {
            // Arrange: the buyer closes the modal while the credit is being authorized; the response still arrives.
            using var uiCts = new CancellationTokenSource();

            creditsAPIClient.AuthorizeUsdNameCreditAsync(Arg.Any<int>(), NAME, Arg.Any<CancellationToken>())
                            .Returns(_ =>
                             {
                                 uiCts.Cancel();
                                 return UniTask.FromResult(CreateAuthorization(NAME_PRICE_WEI));
                             });

            // Act
            NameClaimResult result = await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), uiCts.Token);

            // Assert: the request did not run on the buyer's token, so its credit is known and released.
            Assert.AreEqual(NameClaimError.Cancelled, result.Error);
            await creditsAPIClient.Received(1).AuthorizeUsdNameCreditAsync(Arg.Any<int>(), NAME, Arg.Is<CancellationToken>(token => token != uiCts.Token));
            await creditsAPIClient.Received(1).ReleaseUsdIntentsAsync(Arg.Is<string[]>(salts => salts.Length == 1 && salts[0] == CREDIT_ID), Arg.Any<CancellationToken>());
            await metaTxRelayer.DidNotReceive().RelayUseCreditsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task KeepTheResultUntilItIsAcknowledged()
        {
            // Act
            await service.ClaimAsync(NAME, new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.IsNull(service.InFlightName);
            Assert.AreEqual(NAME, service.LastResultName);
            Assert.AreEqual(NameClaimStatus.Registered, service.LastResult.Status);

            // Act
            service.AcknowledgeResult();

            // Assert
            Assert.IsNull(service.LastResultName);
        }

        [Test]
        public async Task RejectAMalformedNameBeforeAskingForARoute()
        {
            // Act
            NameClaimResult result = await service.ClaimAsync("not valid", new NameClaimQuote(NAME_PRICE_CENTS, NAME_PRICE_CREDITS), CancellationToken.None);

            // Assert
            Assert.AreEqual(NameClaimError.NameUnavailable, result.Error);
            await creditsAPIClient.DidNotReceive().GetNameRouteAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        private static NameCreditRouteResponse CreateRoute() =>
            new ()
            {
                externalCall = new NameRouteExternalCall
                {
                    target = ROUTE_TARGET,
                    selector = ROUTE_SELECTOR,
                    data = ROUTE_DATA,
                    expiresAt = ROUTE_EXPIRES_AT,
                    salt = ROUTE_SALT,
                },
                customExternalCallSignature = ROUTE_SIGNATURE,
            };

        private static AuthorizeCreditResponse CreateAuthorization(string manaCapWei) =>
            new ()
            {
                credit = new AuthorizedCredit
                {
                    id = CREDIT_ID,
                    amount = manaCapWei,
                    availableAmount = manaCapWei,
                    expiresAt = 1767225600,
                    signature = "0x" + new string('b', 130),
                    contract = "0x8b3a40ca1b6f5cafc99d112a4d02e897d1fd8cc5",
                },
                maxCreditedValue = manaCapWei,
                usdCents = NAME_PRICE_CENTS,
                oracleRate = MANA_USD_RATE.ToString(),
            };
    }
}
