using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Threading;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase.Names
{
    public enum AcrossFillOutcome
    {
        /// <summary>The deposit was filled and its destination actions ran: the NAME was registered.</summary>
        Registered,

        /// <summary>Filled with failed actions, refunded or expired: the NAME was not registered.</summary>
        NotRegistered,

        /// <summary>Still in flight after the polling window, or filled without reporting its actions.</summary>
        Unknown,
    }

    /// <summary>
    ///     Waits for the Across bridge to deliver a Polygon deposit on Ethereum, where the destination actions
    ///     register the NAME. Port of the shop web app's lib/names.ts pollAcrossNameStatus.
    /// </summary>
    public class AcrossBridgeStatusPoller
    {
        private const string STATUS_FILLED = "filled";
        private const string STATUS_REFUNDED = "refunded";
        private const string STATUS_EXPIRED = "expired";
        private const int MAX_ATTEMPTS = 60;

        private static readonly TimeSpan POLL_INTERVAL = TimeSpan.FromSeconds(10);

        private readonly IWebRequestController webRequestController;
        private readonly CreditsChainConfig chainConfig;

        public AcrossBridgeStatusPoller(IWebRequestController webRequestController, CreditsChainConfig chainConfig)
        {
            this.webRequestController = webRequestController;
            this.chainConfig = chainConfig;
        }

        public virtual async UniTask<AcrossFillOutcome> WaitForFillAsync(string originTxHash, CancellationToken ct)
        {
            var url = $"{chainConfig.AcrossDepositStatusUrl}?originChainId={chainConfig.ChainId}&depositTxHash={originTxHash}";

            for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                if (ct.IsCancellationRequested)
                    return AcrossFillOutcome.Unknown;

                try
                {
                    AcrossDepositStatus status = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.CREDITS_PURCHASE)
                                                                           .CreateFromJson<AcrossDepositStatus>(WRJsonParser.Newtonsoft);

                    AcrossFillOutcome? outcome = Interpret(status.status, status.actionsSucceeded);

                    if (outcome != null)
                        return outcome.Value;
                }
                catch (OperationCanceledException) { return AcrossFillOutcome.Unknown; }
                catch (Exception e)
                {
                    // A deposit that is not indexed yet answers with an error: keep polling.
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Across status for {originTxHash} not available yet: {e.Message}");
                }

                if (await UniTask.Delay(POLL_INTERVAL, cancellationToken: ct).SuppressCancellationThrow())
                    return AcrossFillOutcome.Unknown;
            }

            return AcrossFillOutcome.Unknown;
        }

        /// <summary>
        ///     Null while the deposit is still pending. A fill that does not report its actions stays Unknown:
        ///     reading it as a success would announce a NAME that may not exist.
        /// </summary>
        internal static AcrossFillOutcome? Interpret(string? status, bool? actionsSucceeded)
        {
            if (string.Equals(status, STATUS_FILLED, StringComparison.OrdinalIgnoreCase))
                return actionsSucceeded switch
                       {
                           true => AcrossFillOutcome.Registered,
                           false => AcrossFillOutcome.NotRegistered,
                           null => AcrossFillOutcome.Unknown,
                       };

            if (string.Equals(status, STATUS_REFUNDED, StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, STATUS_EXPIRED, StringComparison.OrdinalIgnoreCase))
                return AcrossFillOutcome.NotRegistered;

            return null;
        }

        // Server schema: Across GET /deposit/status (Server/shop/app/src/lib/names.ts pollAcrossNameStatus).
        private class AcrossDepositStatus
        {
            public string? status;
            public bool? actionsSucceeded;
        }
    }
}
