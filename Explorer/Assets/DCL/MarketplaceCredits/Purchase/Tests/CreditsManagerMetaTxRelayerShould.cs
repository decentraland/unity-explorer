using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.Web3.Chains;
using DCL.WebRequests;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    /// <summary>
    ///     The meta-transaction relay: which nonce gets signed, and which failures prove the transaction never
    ///     reached the chain.
    /// </summary>
    public class CreditsManagerMetaTxRelayerShould
    {
        private const string BUYER = "0x99995f38fc9d786eab5c3a1b1c4e6ae5f4e99999";
        private const string CALLDATA = "0xabcdef";
        private const string SIGNATURE = "0x1234";
        private const string TX_HASH = "0x1122";
        private const string RELAYER_URL = "https://relayer.example.com/v1/transactions";
        private const string ETH_CALL_METHOD = "eth_call";
        private const string ETH_SIGN_TYPED_DATA_METHOD = "eth_signTypedData_v4";

        private IEthereumApi ethereumApi = null!;
        private IWebRequestController webRequestController = null!;
        private CreditsManagerMetaTxRelayer relayer = null!;
        private BigInteger onChainNonce;
        private string? signedTypedData;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            ethereumApi = Substitute.For<IEthereumApi>();
            webRequestController = Substitute.For<IWebRequestController>();
            IDecentralandUrlsSource urlsSource = Substitute.For<IDecentralandUrlsSource>();
            urlsSource.Url(DecentralandUrl.MetaTransactionServer).Returns(RELAYER_URL);

            onChainNonce = new BigInteger(5);
            signedTypedData = null;
            cts = new CancellationTokenSource();

            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(info => UniTask.FromResult(Respond(info.Arg<EthApiRequest>())));

            StubRelayerResponse(_ => UniTask.FromResult<JObject?>(new JObject { ["txHash"] = TX_HASH }));

            relayer = new CreditsManagerMetaTxRelayer(ethereumApi, webRequestController, urlsSource, new CreditsChainConfig(EthereumNetwork.Sepolia));
        }

        [TearDown]
        public void TearDown() =>
            cts.Dispose();

        private EthApiResponse Respond(EthApiRequest request)
        {
            string result;

            if (request.method == ETH_CALL_METHOD)
                result = $"0x{onChainNonce.ToString("x").PadLeft(64, '0')}";
            else if (request.method == ETH_SIGN_TYPED_DATA_METHOD)
            {
                signedTypedData = (string)request.@params[1];
                result = SIGNATURE;
            }
            else
                throw new InvalidOperationException($"Unexpected method {request.method}");

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = result,
            };
        }

        private void StubRelayerResponse(Func<NSubstitute.Core.CallInfo, UniTask<JObject?>> respond) =>
            webRequestController.SendAsync<GenericPostRequest, GenericPostArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<JObject, GenericPostRequest>, JObject>(default, default)
                                .ReturnsForAnyArgs(respond);

        private string SignedNonce() =>
            JObject.Parse(signedTypedData!)["message"]!["nonce"]!.ToString();

        [Test]
        public async Task SignTheFloorWhenTheContractStillReportsAnOlderNonce()
        {
            // Act
            RelayResult result = await relayer.RelayUseCreditsAsync(BUYER, CALLDATA, new BigInteger(7), CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.AreEqual(RelayOutcome.Broadcast, result.Outcome);
            Assert.AreEqual(TX_HASH, result.TxHash);
            Assert.AreEqual("7", SignedNonce());
            Assert.AreEqual(new BigInteger(7), result.Nonce);
        }

        [Test]
        public async Task SignTheContractNonceWhenItIsAheadOfTheFloor()
        {
            // Arrange
            onChainNonce = new BigInteger(9);

            // Act
            RelayResult result = await relayer.RelayUseCreditsAsync(BUYER, CALLDATA, new BigInteger(7), CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.AreEqual("9", SignedNonce());
            Assert.AreEqual(new BigInteger(9), result.Nonce);
        }

        [Test]
        public void RethrowACancellationDuringTheSignatureWithoutRelayingAnything()
        {
            // Arrange: the buyer closes the popup while the wallet is asking for the signature.
            ethereumApi.SendAsync(Arg.Is<EthApiRequest>(request => request.method == ETH_SIGN_TYPED_DATA_METHOD), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(_ =>
                        {
                            cts.Cancel();
                            throw new OperationCanceledException(cts.Token);
                        });

            // Act and assert
            Assert.CatchAsync<OperationCanceledException>(async () => await relayer.RelayUseCreditsAsync(BUYER, CALLDATA, BigInteger.MinusOne, cts.Token, CancellationToken.None));

            webRequestController.DidNotReceiveWithAnyArgs()
                                .SendAsync<GenericPostRequest, GenericPostArguments, GenericDownloadHandlerUtils.CreateFromJsonOp<JObject, GenericPostRequest>, JObject>(default, default);
        }

        [Test]
        public async Task ReportAnAmbiguousBroadcastWhenTheRelayerRequestIsCancelled()
        {
            // Arrange: the signed transaction may already have reached the relayer when the wait is cancelled.
            StubRelayerResponse(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

            // Act
            RelayResult result = await relayer.RelayUseCreditsAsync(BUYER, CALLDATA, BigInteger.MinusOne, CancellationToken.None, cts.Token);

            // Assert
            Assert.AreEqual(RelayOutcome.AmbiguousBroadcast, result.Outcome);
            Assert.AreEqual(new BigInteger(5), result.Nonce);
        }

        [Test]
        public async Task RejectWhenTheRelayerAnswersWithoutATransactionHash()
        {
            // Arrange
            StubRelayerResponse(_ => UniTask.FromResult<JObject?>(new JObject { ["message"] = "quota exceeded" }));

            // Act
            RelayResult result = await relayer.RelayUseCreditsAsync(BUYER, CALLDATA, BigInteger.MinusOne, CancellationToken.None, CancellationToken.None);

            // Assert
            Assert.AreEqual(RelayOutcome.RelayerRejected, result.Outcome);
            Assert.AreEqual("quota exceeded", result.Message);
        }

        [TestCase(400L, ExpectedResult = true)]
        [TestCase(429L, ExpectedResult = true)]
        [TestCase(499L, ExpectedResult = true)]
        [TestCase(500L, ExpectedResult = false)]
        [TestCase(502L, ExpectedResult = false)]
        [TestCase(504L, ExpectedResult = false)]
        [TestCase(0L, ExpectedResult = false)]
        public bool TreatOnlyAClientErrorAsAProvenRejection(long responseCode) =>
            CreditsManagerMetaTxRelayer.IsProvenRejection(responseCode);
    }
}
