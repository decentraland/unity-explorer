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
        Confirmed,
        Reverted,
        Pending,
    }

    // This poller waits for a broadcast transaction to land on Polygon, this is needed to confirm or not if a transaction has been confirmed or not
    public class PolygonSettlementPoller
    {
        private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(5);
        private const string GET_TRANSACTION_METHOD = "eth_getTransactionReceipt";
        private const string CONFIRMED_STATUS = "0x1";
        private const string REVERTED_STATUS = "0x0";
        private const string STATUS_FIELD = "status";

        private readonly IEthereumApi ethereumApi;
        private readonly CreditsChainConfig chainConfig;

        public PolygonSettlementPoller(IEthereumApi ethereumApi, CreditsChainConfig chainConfig)
        {
            this.ethereumApi = ethereumApi;
            this.chainConfig = chainConfig;
        }

        public virtual async UniTask<SettlementOutcome> WaitForSettlementAsync(string txHash, TimeSpan timeout, CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested)
                    return SettlementOutcome.Pending;

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
                            return SettlementOutcome.Confirmed;

                        if (string.Equals(status, REVERTED_STATUS, StringComparison.OrdinalIgnoreCase))
                            return SettlementOutcome.Reverted;
                    }
                }
                catch (OperationCanceledException)
                {
                    return SettlementOutcome.Pending;
                }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Receipt poll failed for {txHash}: {e.Message}");
                }

                bool cancelled = await UniTask.Delay(POLL_INTERVAL, cancellationToken: ct).SuppressCancellationThrow();

                if (cancelled)
                    return SettlementOutcome.Pending;
            }

            return SettlementOutcome.Pending;
        }
    }
}
