using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public enum SettlementOutcome
    {
        CONFIRMED,
        REVERTED,
        PENDING,
    }

    // This poller waits for a broadcast transaction to land on Polygon, this is needed to confirm or not if a transaction has been confirmed or not
    public class PolygonSettlementPoller
    {
        private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(5);
        private static readonly string GET_TRANSACTION_METHOD = "eth_getTransactionReceipt";
        private static readonly string CONFIRMED_STATUS = "0x1";
        private static readonly string REVERTED_STATUS = "0x0";
        private static readonly string STATUS_FIELD = "status";

        private readonly IEthereumApi ethereumApi;
        private readonly CreditsChainConfig chainConfig;

        public PolygonSettlementPoller(IEthereumApi ethereumApi, CreditsChainConfig chainConfig)
        {
            this.ethereumApi = ethereumApi;
            this.chainConfig = chainConfig;
        }

        public async UniTask<SettlementOutcome> WaitForSettlementAsync(string txHash, TimeSpan timeout, CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested)
                    return SettlementOutcome.PENDING;

                try
                {
                    var request = new EthApiRequest
                    {
                        readonlyNetwork = chainConfig.ReadonlyNetwork,
                        id = Guid.NewGuid().GetHashCode(),
                        method = GET_TRANSACTION_METHOD,
                        @params = new object[] { txHash },
                    };

                    EthApiResponse response = await ethereumApi.SendAsync(request, Web3RequestSource.Internal, ct);

                    if (response.result is JObject receipt)
                    {
                        string? status = receipt[STATUS_FIELD]?.ToString();

                        if (string.Equals(status, CONFIRMED_STATUS, StringComparison.OrdinalIgnoreCase))
                            return SettlementOutcome.CONFIRMED;

                        if (string.Equals(status, REVERTED_STATUS, StringComparison.OrdinalIgnoreCase))
                            return SettlementOutcome.REVERTED;
                    }
                }
                catch (OperationCanceledException)
                {
                    return SettlementOutcome.PENDING;
                }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Receipt poll failed for {txHash}: {e.Message}");
                }

                bool cancelled = await UniTask.Delay(POLL_INTERVAL, cancellationToken: ct).SuppressCancellationThrow();

                if (cancelled)
                    return SettlementOutcome.PENDING;
            }

            return SettlementOutcome.PENDING;
        }
    }
}
