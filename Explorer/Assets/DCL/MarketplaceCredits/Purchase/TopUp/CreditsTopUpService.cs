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
            Abandoned,
        }

        private static readonly TimeSpan FOREGROUND_POLL_INTERVAL = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan FOREGROUND_POLL_TIMEOUT = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan BACKGROUND_POLL_INTERVAL = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan BACKGROUND_POLL_TIMEOUT = TimeSpan.FromMinutes(10);

        private readonly MarketplaceCreditsAPIClient creditsApiClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly TimeSpan foregroundPollInterval;
        private readonly TimeSpan foregroundPollTimeout;
        private readonly TimeSpan backgroundPollInterval;
        private readonly TimeSpan backgroundPollTimeout;

        private CancellationTokenSource? cts;

        public CreditsTopUpStatus CurrentStatus { get; private set; } = CreditsTopUpStatus.Idle();

        public bool IsOrderInFlight =>
            CurrentStatus.Stage is CreditsTopUpStage.CreatingCheckout or CreditsTopUpStage.WaitingForPayment;

        public event Action<CreditsTopUpStatus>? StatusChanged;

        public CreditsTopUpService(
            MarketplaceCreditsAPIClient creditsApiClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser)
            : this(creditsApiClient, identityCache, webBrowser,
                FOREGROUND_POLL_INTERVAL, FOREGROUND_POLL_TIMEOUT, BACKGROUND_POLL_INTERVAL, BACKGROUND_POLL_TIMEOUT) { }

        public CreditsTopUpService(
            MarketplaceCreditsAPIClient creditsApiClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            TimeSpan foregroundPollInterval,
            TimeSpan foregroundPollTimeout,
            TimeSpan backgroundPollInterval,
            TimeSpan backgroundPollTimeout)
        {
            this.creditsApiClient = creditsApiClient;
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.foregroundPollInterval = foregroundPollInterval;
            this.foregroundPollTimeout = foregroundPollTimeout;
            this.backgroundPollInterval = backgroundPollInterval;
            this.backgroundPollTimeout = backgroundPollTimeout;
        }

        public void Dispose() =>
            cts.SafeCancelAndDispose();

        public void StartTopUp(CreditPack pack)
        {
            if (IsOrderInFlight)
                return;

            cts = cts.SafeRestart();
            RunTopUpAsync(pack, cts.Token).Forget();
        }

        public void CancelTopUp()
        {
            if (CurrentStatus.Stage == CreditsTopUpStage.Idle)
                return;

            // The order is only abandoned client-side (there is no server abort endpoint): a payment
            // already completed in the browser is still credited by the server, just no longer watched.
            cts.SafeCancelAndDispose();
            cts = null;
            SetStatus(CreditsTopUpStatus.Idle());
        }

        public void AcknowledgeTerminalState()
        {
            if (CurrentStatus.Stage is CreditsTopUpStage.Credited or CreditsTopUpStage.Failed or CreditsTopUpStage.Abandoned)
                SetStatus(CreditsTopUpStatus.Idle());
        }

        private async UniTaskVoid RunTopUpAsync(CreditPack pack, CancellationToken ct)
        {
            try
            {
                SetStatus(CreditsTopUpStatus.CreatingCheckout(pack));

                EnumResult<CheckoutResponse, CreditsCheckoutError> checkoutResult = await creditsApiClient.CreateCheckoutAsync(pack.Id, ct);

                if (ct.IsCancellationRequested)
                    return;

                if (checkoutResult.Error?.State == CreditsCheckoutError.Cancelled)
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

                (PollOutcome outcome, CreditsOrderStatusResponse order) = await PollOrderAsync(orderId, foregroundPollInterval, foregroundPollTimeout, ct);

                // On cancellation the status is owned by whoever cancelled (CancelTopUp sets Idle);
                // writing here would overwrite it.
                if (outcome == PollOutcome.Cancelled || ct.IsCancellationRequested)
                    return;

                if (outcome == PollOutcome.TimedOut)
                {
                    SetStatus(CreditsTopUpStatus.PendingTimeout(pack, orderId));
                    (outcome, order) = await PollOrderAsync(orderId, backgroundPollInterval, backgroundPollTimeout, ct);

                    if (outcome == PollOutcome.Cancelled || ct.IsCancellationRequested)
                        return;
                }

                switch (outcome)
                {
                    case PollOutcome.Credited:
                        await RefreshBalanceAsync(ct);

                        if (!ct.IsCancellationRequested)
                            SetStatus(CreditsTopUpStatus.Credited(pack, orderId, order.creditsGranted, order.newBalance));

                        break;
                    case PollOutcome.Failed:
                        SetStatus(CreditsTopUpStatus.GrantFailed(pack, orderId, order.error));
                        break;
                    case PollOutcome.Abandoned:
                        SetStatus(CreditsTopUpStatus.Abandoned(pack, orderId));
                        break;
                }
            }
            catch (OperationCanceledException) { }
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

                EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError> result = await creditsApiClient.GetCheckoutOrderAsync(orderId, ct);

                if (result.Success)
                {
                    switch (result.Value.status)
                    {
                        case CreditsOrderStatusResponse.STATUS_CREDITED:
                            return (PollOutcome.Credited, result.Value);
                        case CreditsOrderStatusResponse.STATUS_FAILED:
                            return (PollOutcome.Failed, result.Value);
                        case CreditsOrderStatusResponse.STATUS_ABANDONED:
                            return (PollOutcome.Abandoned, result.Value);
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
                await creditsApiClient.GetUserCreditsAsync(identity.Address, ct);
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
