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
            CREDITED,
            FAILED,
            TIMED_OUT,
            CANCELLED,
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
        private bool pendingAcknowledged;

        public CreditsTopUpStatus CurrentStatus { get; private set; } = CreditsTopUpStatus.Idle();

        public bool IsOrderInFlight =>
            CurrentStatus.Stage is CreditsTopUpStage.CREATING_CHECKOUT or CreditsTopUpStage.WAITING_FOR_PAYMENT;

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
        }

        public void StartTopUp(CreditPack pack)
        {
            if (IsOrderInFlight)
                return;

            pendingAcknowledged = false;
            cts = cts.SafeRestart();
            RunTopUpAsync(pack, cts.Token).Forget();
        }

        public void AcknowledgeTerminalState()
        {
            switch (CurrentStatus.Stage)
            {
                case CreditsTopUpStage.CREDITED:
                case CreditsTopUpStage.FAILED:
                    SetStatus(CreditsTopUpStatus.Idle());
                    break;
                case CreditsTopUpStage.PENDING_TIMEOUT:
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

                (PollOutcome outcome, CreditsOrderStatusResponse order) = await PollOrderAsync(orderId, foregroundPollInterval, foregroundPollTimeout, ct);

                if (outcome == PollOutcome.TIMED_OUT)
                {
                    SetStatus(CreditsTopUpStatus.PendingTimeout(pack, orderId));
                    (outcome, order) = await PollOrderAsync(orderId, backgroundPollInterval, backgroundPollTimeout, ct);
                }

                switch (outcome)
                {
                    case PollOutcome.CREDITED:
                        await RefreshBalanceAsync(ct);

                        if (!pendingAcknowledged)
                            SetStatus(CreditsTopUpStatus.Credited(pack, orderId, order.creditsGranted, order.newBalance));
                        break;
                    case PollOutcome.FAILED:
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
                    return (PollOutcome.CANCELLED, default(CreditsOrderStatusResponse));

                EnumResult<CreditsOrderStatusResponse, CreditsOrderPollError> result = await creditsAPIClient.GetCheckoutOrderAsync(orderId, ct);

                if (result.Success)
                {
                    switch (result.Value.status)
                    {
                        case CreditsOrderStatusResponse.STATUS_CREDITED:
                            return (PollOutcome.CREDITED, result.Value);
                        case CreditsOrderStatusResponse.STATUS_FAILED:
                            return (PollOutcome.FAILED, result.Value);
                    }
                }
                else
                {
                    if (result.Error!.Value.State == CreditsOrderPollError.Cancelled)
                        return (PollOutcome.CANCELLED, default(CreditsOrderStatusResponse));

                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Order status poll failed for {orderId}: {result.Error.Value.State} {result.Error.Value.Message}");
                }

                bool cancelled = await UniTask.Delay(interval, cancellationToken: ct).SuppressCancellationThrow();

                if (cancelled)
                    return (PollOutcome.CANCELLED, default(CreditsOrderStatusResponse));
            }

            return (PollOutcome.TIMED_OUT, default(CreditsOrderStatusResponse));
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
