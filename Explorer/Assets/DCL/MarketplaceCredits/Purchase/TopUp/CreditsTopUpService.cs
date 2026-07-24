using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public class CreditsTopUpService : ICreditsTopUpService
    {
        private enum PollOutcome
        {
            Credited,
            Failed,
            TimedOut,
            Cancelled,
        }

        private static readonly TimeSpan FOREGROUND_POLL_INTERVAL = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan FOREGROUND_POLL_TIMEOUT = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan BACKGROUND_POLL_INTERVAL = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan BACKGROUND_POLL_TIMEOUT = TimeSpan.FromMinutes(10);

        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly TimeSpan foregroundPollInterval;
        private readonly TimeSpan foregroundPollTimeout;
        private readonly TimeSpan backgroundPollInterval;
        private readonly TimeSpan backgroundPollTimeout;

        private CancellationTokenSource? cts;
        private CancellationTokenSource? skipForegroundCts;
        private bool pendingAcknowledged;

        public CreditsTopUpStatus CurrentStatus { get; private set; } = CreditsTopUpStatus.Idle();

        public bool IsOrderInFlight =>
            CurrentStatus.Stage is CreditsTopUpStage.CreatingCheckout or CreditsTopUpStage.WaitingForPayment;

        public event Action<CreditsTopUpStatus>? StatusChanged;

        public CreditsTopUpService(
            MarketplaceCreditsAPIClient creditsAPIClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser)
            : this(creditsAPIClient, identityCache, webBrowser,
                FOREGROUND_POLL_INTERVAL, FOREGROUND_POLL_TIMEOUT, BACKGROUND_POLL_INTERVAL, BACKGROUND_POLL_TIMEOUT) { }

        public CreditsTopUpService(
            MarketplaceCreditsAPIClient creditsAPIClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            TimeSpan foregroundPollInterval,
            TimeSpan foregroundPollTimeout,
            TimeSpan backgroundPollInterval,
            TimeSpan backgroundPollTimeout)
        {
            this.creditsAPIClient = creditsAPIClient;
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.foregroundPollInterval = foregroundPollInterval;
            this.foregroundPollTimeout = foregroundPollTimeout;
            this.backgroundPollInterval = backgroundPollInterval;
            this.backgroundPollTimeout = backgroundPollTimeout;
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            skipForegroundCts.SafeCancelAndDispose();
        }

        public void StartTopUp(CreditPack pack)
        {
            if (IsOrderInFlight)
                return;

            pendingAcknowledged = false;
            cts = cts.SafeRestart();
            RunTopUpAsync(pack, cts.Token).Forget();
        }

        public void StopWaitingForBrowser()
        {
            if (CurrentStatus.Stage != CreditsTopUpStage.WaitingForPayment)
                return;

            // Cancels only the foreground poll; RunTopUpAsync then hands the order off to the
            // background poll (PENDING_TIMEOUT), so a payment completed later still gets credited.
            skipForegroundCts?.Cancel();
        }

        public void AcknowledgeTerminalState()
        {
            switch (CurrentStatus.Stage)
            {
                case CreditsTopUpStage.Credited:
                case CreditsTopUpStage.Failed:
                    SetStatus(CreditsTopUpStatus.Idle());
                    break;
                case CreditsTopUpStage.PendingTimeout:
                    pendingAcknowledged = true;
                    SetStatus(CreditsTopUpStatus.Idle());
                    break;
            }
        }

        private async UniTaskVoid RunTopUpAsync(CreditPack pack, CancellationToken ct)
        {
            try
            {
                SetStatus(CreditsTopUpStatus.CreatingCheckout(pack));

                EnumResult<CheckoutResponse, CreditsCheckoutError> checkoutResult = await creditsAPIClient.CreateCheckoutAsync(pack.Id, ct);

                if (ct.IsCancellationRequested || checkoutResult.Error?.State == CreditsCheckoutError.Cancelled)
                {
                    SetStatus(CreditsTopUpStatus.Idle());
                    return;
                }

                if (!checkoutResult.Success)
                {
                    (CreditsCheckoutError error, string message) = checkoutResult.Error!.Value;
                    SetStatus(CreditsTopUpStatus.CheckoutFailed(pack, error, message));
                    return;
                }

                string orderId = checkoutResult.Value.orderId;

                webBrowser.OpenUrlMainThreadOnly(checkoutResult.Value.url);
                SetStatus(CreditsTopUpStatus.WaitingForPayment(pack, orderId));

                skipForegroundCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                (PollOutcome outcome, CreditsOrderStatusResponse order) = await PollOrderAsync(orderId, foregroundPollInterval, foregroundPollTimeout, skipForegroundCts.Token);
                skipForegroundCts.SafeCancelAndDispose();
                skipForegroundCts = null;

                // Only the skip token fired (the user stopped waiting), not the outer ct: fall through to the background poll.
                if (outcome == PollOutcome.Cancelled && !ct.IsCancellationRequested)
                    outcome = PollOutcome.TimedOut;

                if (outcome == PollOutcome.TimedOut)
                {
                    SetStatus(CreditsTopUpStatus.PendingTimeout(pack, orderId));
                    (outcome, order) = await PollOrderAsync(orderId, backgroundPollInterval, backgroundPollTimeout, ct);
                }

                switch (outcome)
                {
                    case PollOutcome.Credited:
                        await RefreshBalanceAsync(ct);

                        if (!pendingAcknowledged)
                            SetStatus(CreditsTopUpStatus.Credited(pack, orderId, order.creditsGranted, order.newBalance));
                        break;
                    case PollOutcome.Failed:
                        if (!pendingAcknowledged)
                            SetStatus(CreditsTopUpStatus.GrantFailed(pack, orderId, order.error));
                        break;
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                SetStatus(CreditsTopUpStatus.CheckoutFailed(pack, CreditsCheckoutError.NetworkError, e.Message));
            }
        }

        private async UniTask<(PollOutcome outcome, CreditsOrderStatusResponse order)> PollOrderAsync(string orderId, TimeSpan interval, TimeSpan timeout, CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (ct.IsCancellationRequested)
                    return (PollOutcome.Cancelled, default(CreditsOrderStatusResponse));

                EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError> result = await creditsAPIClient.GetCheckoutOrderAsync(orderId, ct);

                if (result.Success)
                {
                    switch (result.Value.status)
                    {
                        case CreditsOrderStatusResponse.STATUS_CREDITED:
                            return (PollOutcome.Credited, result.Value);
                        case CreditsOrderStatusResponse.STATUS_FAILED:
                            return (PollOutcome.Failed, result.Value);
                    }
                }
                else
                {
                    if (result.Error!.Value.State == CreditsOrderPollError.Cancelled)
                        return (PollOutcome.Cancelled, default(CreditsOrderStatusResponse));

                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Order status poll failed for {orderId}: {result.Error.Value.State} {result.Error.Value.Message}");
                }

                bool cancelled = await UniTask.Delay(interval, cancellationToken: ct).SuppressCancellationThrow();

                if (cancelled)
                    return (PollOutcome.Cancelled, default(CreditsOrderStatusResponse));
            }

            return (PollOutcome.TimedOut, default(CreditsOrderStatusResponse));
        }

        private async UniTask RefreshBalanceAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return;

            try
            {
                await creditsAPIClient.GetUserCreditsAsync(identity.Address, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Balance refresh after top-up failed: {e.Message}");
            }
        }

        private void SetStatus(CreditsTopUpStatus status)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
        }
    }
}
