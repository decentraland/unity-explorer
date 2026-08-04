using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class ManaUsdRateReaderShould
    {
        private const string MARKETPLACE = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string AGGREGATOR_HEX = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private const int ORACLE_DECIMALS = 8;
        private const long MANA_USD_RATE = 25_000_000;

        // The reader sends the 4-byte sighash of each parameterless call as its whole calldata.
        private static readonly string AGGREGATOR_CALLDATA = CreditsTradeEncoder.SighashOf(@"[{""name"":""manaUsdAggregator"",""type"":""function"",""inputs"":[],""outputs"":[{""name"":"""",""type"":""address""}]}]");
        private static readonly string DECIMALS_CALLDATA = CreditsTradeEncoder.SighashOf(@"[{""name"":""decimals"",""type"":""function"",""inputs"":[],""outputs"":[{""name"":"""",""type"":""uint8""}]}]");
        private static readonly string LATEST_ROUND_DATA_CALLDATA = CreditsTradeEncoder.SighashOf(@"[{""name"":""latestRoundData"",""type"":""function"",""inputs"":[],""outputs"":[{""name"":""roundId"",""type"":""uint80""},{""name"":""answer"",""type"":""int256""},{""name"":""startedAt"",""type"":""uint256""},{""name"":""updatedAt"",""type"":""uint256""},{""name"":""answeredInRound"",""type"":""uint80""}]}]");

        private IEthereumApi ethereumApi = null!;
        private ManaUsdRateReader reader = null!;
        private List<string> sentCalldata = null!;

        private BigInteger answer;
        private BigInteger roundId;
        private BigInteger answeredInRound;
        private long updatedAtSeconds;

        [SetUp]
        public void SetUp()
        {
            ethereumApi = Substitute.For<IEthereumApi>();
            reader = new ManaUsdRateReader(ethereumApi, new CreditsChainConfig(DecentralandEnvironment.Zone));
            sentCalldata = new List<string>();

            answer = MANA_USD_RATE;
            roundId = 1;
            answeredInRound = 1;
            updatedAtSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(info => UniTask.FromResult(Respond(info.Arg<EthApiRequest>())));
        }

        private EthApiResponse Respond(EthApiRequest request)
        {
            var call = (JObject)request.@params[0];
            string data = call["data"]!.ToString();
            sentCalldata.Add(data);

            string result;

            if (data == AGGREGATOR_CALLDATA)
                result = $"0x{new string('0', 24)}{AGGREGATOR_HEX}";
            else if (data == DECIMALS_CALLDATA)
                result = $"0x{Word(ORACLE_DECIMALS)}";
            else if (data == LATEST_ROUND_DATA_CALLDATA)
                result = $"0x{Word(roundId)}{Word(answer)}{Word(0)}{Word(updatedAtSeconds)}{Word(answeredInRound)}";
            else
                throw new InvalidOperationException($"Unexpected calldata {data}");

            return new EthApiResponse
            {
                id = request.id,
                jsonrpc = "2.0",
                result = result,
            };
        }

        private static string Word(BigInteger value) =>
            value.ToString("x").PadLeft(64, '0');

        [Test]
        public async Task ReadTheRateThroughTheAggregatorTheMarketplaceExposes()
        {
            // Act
            ManaUsdRate rate = await reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            // Assert: aggregator lookup, then decimals + latestRoundData.
            Assert.AreEqual(new BigInteger(MANA_USD_RATE), rate.Rate);
            Assert.AreEqual(ORACLE_DECIMALS, rate.Decimals);
            Assert.AreEqual(3, sentCalldata.Count);
        }

        [Test]
        public async Task ServeASecondReadFromTheCacheWithinTheTtl()
        {
            // Act
            ManaUsdRate first = await reader.ReadAsync(MARKETPLACE, CancellationToken.None);
            ManaUsdRate second = await reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            // Assert
            Assert.AreEqual(first.Rate, second.Rate);
            Assert.AreEqual(3, sentCalldata.Count);
        }

        [Test]
        public async Task RefetchOnlyTheLatestRoundOnceTheTtlExpires()
        {
            // Arrange: a zero TTL expires every read; the aggregator address and decimals never change, so
            // only latestRoundData goes back on the wire.
            reader = new ManaUsdRateReader(ethereumApi, new CreditsChainConfig(DecentralandEnvironment.Zone), TimeSpan.Zero);

            // Act
            await reader.ReadAsync(MARKETPLACE, CancellationToken.None);
            await reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            // Assert
            Assert.AreEqual(4, sentCalldata.Count);
            Assert.AreEqual(LATEST_ROUND_DATA_CALLDATA, sentCalldata[3]);
        }

        [Test]
        public async Task CoalesceConcurrentReadsIntoASingleFetch()
        {
            // Arrange: responses held open so both reads are in flight before any call resolves.
            var pendingCalls = new List<(EthApiRequest request, UniTaskCompletionSource<EthApiResponse> response)>();

            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(info =>
                        {
                            var response = new UniTaskCompletionSource<EthApiResponse>();
                            pendingCalls.Add((info.Arg<EthApiRequest>(), response));
                            return response.Task;
                        });

            // Act
            UniTask<ManaUsdRate> first = reader.ReadAsync(MARKETPLACE, CancellationToken.None);
            UniTask<ManaUsdRate> second = reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            while (pendingCalls.Count > 0)
            {
                (EthApiRequest request, UniTaskCompletionSource<EthApiResponse> response) = pendingCalls[0];
                pendingCalls.RemoveAt(0);
                response.TrySetResult(Respond(request));
            }

            ManaUsdRate firstRate = await first;
            ManaUsdRate secondRate = await second;

            // Assert
            Assert.AreEqual(new BigInteger(MANA_USD_RATE), firstRate.Rate);
            Assert.AreEqual(firstRate.Rate, secondRate.Rate);
            Assert.AreEqual(3, sentCalldata.Count);
        }

        [Test]
        public async Task KeepTheSharedFetchAliveWhenOneReaderCancels()
        {
            // Arrange
            var pendingCalls = new List<(EthApiRequest request, UniTaskCompletionSource<EthApiResponse> response)>();

            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(info =>
                        {
                            var response = new UniTaskCompletionSource<EthApiResponse>();
                            pendingCalls.Add((info.Arg<EthApiRequest>(), response));
                            return response.Task;
                        });

            using var cts = new CancellationTokenSource();
            UniTask<ManaUsdRate> cancelled = reader.ReadAsync(MARKETPLACE, cts.Token);
            UniTask<ManaUsdRate> surviving = reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            // Act
            cts.Cancel();

            try
            {
                await cancelled;
                Assert.Fail("Expected OperationCanceledException");
            }
            catch (OperationCanceledException) { }

            while (pendingCalls.Count > 0)
            {
                (EthApiRequest request, UniTaskCompletionSource<EthApiResponse> response) = pendingCalls[0];
                pendingCalls.RemoveAt(0);
                response.TrySetResult(Respond(request));
            }

            ManaUsdRate rate = await surviving;

            // Assert
            Assert.AreEqual(new BigInteger(MANA_USD_RATE), rate.Rate);
        }

        [Test]
        public async Task ThrowWhenTheRoundIsStale()
        {
            // Arrange: older than the 90 000s staleness bound.
            updatedAtSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 100_000;

            // Act & Assert
            try
            {
                await reader.ReadAsync(MARKETPLACE, CancellationToken.None);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException e) { StringAssert.Contains("stale", e.Message); }
        }

        [Test]
        public async Task ThrowWhenTheRateIsNonPositive()
        {
            // Arrange
            answer = 0;

            // Act & Assert
            try
            {
                await reader.ReadAsync(MARKETPLACE, CancellationToken.None);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException e) { StringAssert.Contains("non-positive", e.Message); }
        }

        [Test]
        public async Task ThrowWhenTheRoundIsIncomplete()
        {
            // Arrange
            roundId = 2;
            answeredInRound = 1;

            // Act & Assert
            try
            {
                await reader.ReadAsync(MARKETPLACE, CancellationToken.None);
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException e) { StringAssert.Contains("incomplete", e.Message); }
        }

        [Test]
        public async Task SwallowPrefetchFailuresWithoutCachingThem()
        {
            // Arrange
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns<UniTask<EthApiResponse>>(_ => throw new Web3Exception("rpc down"));

            // Act: the warm-up failure stays internal...
            await reader.PrefetchAsync(MARKETPLACE);

            // ...and the next read fetches for itself once the transport recovers.
            ethereumApi.SendAsync(Arg.Any<EthApiRequest>(), Arg.Any<Web3RequestSource>(), Arg.Any<CancellationToken>())
                       .Returns(info => UniTask.FromResult(Respond(info.Arg<EthApiRequest>())));

            ManaUsdRate rate = await reader.ReadAsync(MARKETPLACE, CancellationToken.None);

            // Assert
            Assert.AreEqual(new BigInteger(MANA_USD_RATE), rate.Rate);
        }
    }
}
