using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     The MANA/USD rate a marketplace settles its USD-pegged trades with: USD per MANA scaled by
    ///     10^<see cref="Decimals" />.
    /// </summary>
    public readonly struct ManaUsdRate
    {
        public readonly BigInteger Rate;
        public readonly int Decimals;

        public ManaUsdRate(BigInteger rate, int decimals)
        {
            Rate = rate;
            Decimals = decimals;
        }
    }

    /// <summary>
    ///     Reads the Chainlink-style MANA/USD aggregator the trade's own marketplace exposes. Port of the shop
    ///     web app's lib/mana-rate.ts readManaUsdRate: the same oracle the on-chain accept converts USD-pegged
    ///     amounts with, so a price quoted here is the price settlement charges.
    /// </summary>
    public class ManaUsdRateReader
    {
        private const string ETH_CALL_METHOD = "eth_call";
        private const string LATEST_BLOCK = "latest";

        // The aggregator's heartbeat is on the order of a day; the extra hour keeps a round that lands right at
        // the heartbeat (or a slightly fast local clock) from rejecting an otherwise healthy feed.
        private const long MAX_STALENESS_SECONDS = 90_000;

        // Bounds the whole read even when the underlying transport has no timeout of its own.
        private static readonly TimeSpan FETCH_TIMEOUT = TimeSpan.FromSeconds(20);

        // A cached rate is at most this old — negligible against the feed's ~1-day heartbeat and the
        // MAX_STALENESS_SECONDS bound, so cache hits need no re-validation.
        private static readonly TimeSpan DEFAULT_RATE_TTL = TimeSpan.FromSeconds(60);

        private const string MANA_USD_AGGREGATOR_ABI = @"[{""name"":""manaUsdAggregator"",""type"":""function"",""inputs"":[],""outputs"":[
            {""name"":"""",""type"":""address""}]}]";

        private const string DECIMALS_ABI = @"[{""name"":""decimals"",""type"":""function"",""inputs"":[],""outputs"":[
            {""name"":"""",""type"":""uint8""}]}]";

        private const string LATEST_ROUND_DATA_ABI = @"[{""name"":""latestRoundData"",""type"":""function"",""inputs"":[],""outputs"":[
            {""name"":""roundId"",""type"":""uint80""},
            {""name"":""answer"",""type"":""int256""},
            {""name"":""startedAt"",""type"":""uint256""},
            {""name"":""updatedAt"",""type"":""uint256""},
            {""name"":""answeredInRound"",""type"":""uint80""}]}]";

        // Parameterless calls: the calldata is just the 4-byte sighash.
        private static readonly string MANA_USD_AGGREGATOR_CALLDATA = CreditsTradeEncoder.SighashOf(MANA_USD_AGGREGATOR_ABI);
        private static readonly string DECIMALS_CALLDATA = CreditsTradeEncoder.SighashOf(DECIMALS_ABI);
        private static readonly string LATEST_ROUND_DATA_CALLDATA = CreditsTradeEncoder.SighashOf(LATEST_ROUND_DATA_ABI);

        private readonly IEthereumApi ethereumApi;
        private readonly CreditsChainConfig chainConfig;
        private readonly TimeSpan rateTtl;
        private readonly object gate = new ();
        private readonly Dictionary<string, string> aggregatorByMarketplace = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> decimalsByAggregator = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (ManaUsdRate rate, DateTime fetchedAtUtc)> rateByMarketplace = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UniTaskCompletionSource<ManaUsdRate>> inFlightByMarketplace = new (StringComparer.OrdinalIgnoreCase);

        public ManaUsdRateReader(IEthereumApi ethereumApi, CreditsChainConfig chainConfig)
            : this(ethereumApi, chainConfig, DEFAULT_RATE_TTL) { }

        public ManaUsdRateReader(IEthereumApi ethereumApi, CreditsChainConfig chainConfig, TimeSpan rateTtl)
        {
            this.ethereumApi = ethereumApi;
            this.chainConfig = chainConfig;
            this.rateTtl = rateTtl;
        }

        /// <summary>
        ///     Throws when the aggregator is unreachable, or its latest round is non-positive, incomplete or
        ///     stale — a bad rate must fail the purchase, never price it. Concurrent readers share one fetch,
        ///     and a rate read within <see cref="rateTtl" /> is served from cache.
        /// </summary>
        public virtual async UniTask<ManaUsdRate> ReadAsync(string marketplaceAddress, CancellationToken ct)
        {
            UniTaskCompletionSource<ManaUsdRate> fetch;

            lock (gate)
            {
                if (rateByMarketplace.TryGetValue(marketplaceAddress, out (ManaUsdRate rate, DateTime fetchedAtUtc) cached)
                    && DateTime.UtcNow - cached.fetchedAtUtc < rateTtl)
                    return cached.rate;

                if (!inFlightByMarketplace.TryGetValue(marketplaceAddress, out fetch))
                {
                    fetch = new UniTaskCompletionSource<ManaUsdRate>();
                    inFlightByMarketplace[marketplaceAddress] = fetch;
                    FetchIntoAsync(marketplaceAddress, fetch).Forget();
                }
            }

            return await fetch.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        ///     Warms the rate cache without surfacing failures: a miss here only means the next
        ///     <see cref="ReadAsync" /> pays the fetch itself.
        /// </summary>
        public virtual async UniTask PrefetchAsync(string marketplaceAddress)
        {
            try { await ReadAsync(marketplaceAddress, CancellationToken.None); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"MANA/USD rate prefetch failed for {marketplaceAddress}: {e.Message}");
            }
        }

        private async UniTaskVoid FetchIntoAsync(string marketplaceAddress, UniTaskCompletionSource<ManaUsdRate> completion)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(FETCH_TIMEOUT);
                ManaUsdRate rate;

                try { rate = await FetchAsync(marketplaceAddress, timeoutCts.Token); }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"MANA/USD rate read for {marketplaceAddress} timed out after {FETCH_TIMEOUT.TotalSeconds:F0}s");
                }

                lock (gate) { rateByMarketplace[marketplaceAddress] = (rate, DateTime.UtcNow); }

                completion.TrySetResult(rate);
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception e) { completion.TrySetException(e); }
            finally
            {
                lock (gate) { inFlightByMarketplace.Remove(marketplaceAddress); }
            }
        }

        private async UniTask<ManaUsdRate> FetchAsync(string marketplaceAddress, CancellationToken ct)
        {
            const int RATE_INDEX = 1;
            const int ROUND_ID_INDEX = 0;
            const int ANSWERED_IN_ROUND_INDEX = 4;
            const int UPDATED_AT_INDEX = 3;

            string? aggregator;
            lock (gate) { aggregatorByMarketplace.TryGetValue(marketplaceAddress, out aggregator); }

            if (aggregator == null)
            {
                aggregator = ToAddress(await CallAsync(marketplaceAddress, MANA_USD_AGGREGATOR_CALLDATA, ct));
                lock (gate) { aggregatorByMarketplace[marketplaceAddress] = aggregator; }
            }

            bool decimalsKnown;
            int decimals;
            lock (gate) { decimalsKnown = decimalsByAggregator.TryGetValue(aggregator, out decimals); }

            string roundHex;

            if (decimalsKnown)
                roundHex = await CallAsync(aggregator, LATEST_ROUND_DATA_CALLDATA, ct);
            else
            {
                (string decimalsHex, string latestRoundHex) = await UniTask.WhenAll(
                    CallAsync(aggregator, DECIMALS_CALLDATA, ct),
                    CallAsync(aggregator, LATEST_ROUND_DATA_CALLDATA, ct));

                decimals = (int)UnsignedWordAt(decimalsHex, 0);
                roundHex = latestRoundHex;

                lock (gate) { decimalsByAggregator[aggregator] = decimals; }
            }

            // answer is int256: parsed signed, so a negative rate surfaces as such instead of a huge positive.
            BigInteger rate = SignedWordAt(roundHex, RATE_INDEX);

            if (rate <= BigInteger.Zero)
                throw new InvalidOperationException($"MANA/USD aggregator {aggregator} returned a non-positive rate");

            BigInteger roundId = UnsignedWordAt(roundHex, ROUND_ID_INDEX);
            BigInteger answeredInRound = UnsignedWordAt(roundHex, ANSWERED_IN_ROUND_INDEX);

            if (answeredInRound < roundId)
                throw new InvalidOperationException($"MANA/USD aggregator {aggregator} round is incomplete (answeredInRound {answeredInRound} < roundId {roundId})");

            BigInteger updatedAt = UnsignedWordAt(roundHex, UPDATED_AT_INDEX);
            BigInteger age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - updatedAt;

            if (updatedAt <= BigInteger.Zero || age > MAX_STALENESS_SECONDS)
                throw new InvalidOperationException($"MANA/USD aggregator {aggregator} rate is stale (age {age}s)");

            return new ManaUsdRate(rate, decimals);
        }

        private async UniTask<string> CallAsync(string contractAddress, string calldata, CancellationToken ct)
        {
            var request = new EthApiRequest
            {
                readonlyNetwork = chainConfig.ReadonlyNetwork,
                id = Guid.NewGuid().GetHashCode(),
                method = ETH_CALL_METHOD,
                @params = new object[]
                {
                    new JObject
                    {
                        ["to"] = contractAddress,
                        ["data"] = calldata,
                    },
                    LATEST_BLOCK,
                },
            };

            EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);
            return response.result?.ToString() ?? throw new InvalidOperationException($"eth_call {calldata} on {contractAddress} returned no result");
        }

        private static string ToAddress(string hex) =>
            $"0x{WordAt(hex, 0).Substring(24)}";

        // A leading zero digit keeps the word positive: these fields are unsigned, so a high bit is magnitude.
        private static BigInteger UnsignedWordAt(string hex, int index) =>
            BigInteger.Parse($"0{WordAt(hex, index)}", NumberStyles.HexNumber);

        private static BigInteger SignedWordAt(string hex, int index) =>
            BigInteger.Parse(WordAt(hex, index), NumberStyles.HexNumber);

        private static string WordAt(string hex, int index)
        {
            const int LENGTH = 64;
            int start = (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0) + index * LENGTH;

            if (hex.Length < start + LENGTH)
                throw new InvalidOperationException($"eth_call returned {hex.Length} hex characters, expected at least {start + LENGTH}");

            return hex.Substring(start, LENGTH);
        }
    }
}
