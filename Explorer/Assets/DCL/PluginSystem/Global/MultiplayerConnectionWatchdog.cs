using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.ErrorPopup;
using DCL.WebRequests;
using LiveKit.Rooms;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Watches multiplayer connectivity (LiveKit rooms + Pulse) while in-world and, when it is lost,
    ///     verifies whether the internet itself is unreachable before surfacing the connection-lost popup.
    ///     <para>
    ///     A dropped socket alone is only a <i>suspicion</i> — LiveKit and Pulse both run their own
    ///     reconnection loops, and a socket can also drop for server-side reasons. So loss is confirmed in two stages:
    ///     <list type="number">
    ///         <item>cheap OS gate via <see cref="Application.internetReachability" /> (Option B);</item>
    ///         <item>an HTTP HEAD probe against a highly-available endpoint (Option A).</item>
    ///     </list>
    ///     The blocking popup is only shown when both multiplayer is down AND the probe confirms we are offline.
    ///     </para>
    /// </summary>
    public class MultiplayerConnectionWatchdog : IDisposable
    {
        private const int POLL_INTERVAL_SECONDS = 3;
        /// <summary>Grace period given to the native reconnection loops before we treat a loss as sustained.</summary>
        private const int SUSTAINED_LOSS_GRACE_SECONDS = 4;
        private const int PROBE_TIMEOUT_SECONDS = 5;

        private readonly IRoomHub roomHub;
        private readonly ITransport pulseTransport;
        private readonly IWebRequestController webRequestController;
        private readonly IMVCManager mvcManager;
        private readonly URLAddress reachabilityProbeUrl;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? popupCts;

        private bool everConnected;
        private bool popupShowing;
        private bool evaluating;

        public MultiplayerConnectionWatchdog(
            IRoomHub roomHub,
            ITransport pulseTransport,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.roomHub = roomHub;
            this.pulseTransport = pulseTransport;
            this.webRequestController = webRequestController;
            this.mvcManager = mvcManager;

            // Raw host endpoint (e.g. https://decentraland.org), no gateway transform / feature-flag dependency — a stable reachability target.
            reachabilityProbeUrl = URLAddress.FromString(decentralandUrlsSource.Probe(DecentralandUrl.Host));
        }

        public void Start()
        {
            lifeCts = lifeCts.SafeRestart();

            // LiveKit exposes real connection events — react to them immediately.
            roomHub.IslandRoom().ConnectionUpdated += OnRoomConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnRoomConnectionUpdated;

            // Pulse exposes no event (its single disconnect handler is owned by the movement bus),
            // so the loop also covers it by polling IsAuthenticated.
            MonitorLoopAsync(lifeCts.Token).Forget();
        }

        public void Dispose()
        {
            roomHub.IslandRoom().ConnectionUpdated -= OnRoomConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnRoomConnectionUpdated;

            lifeCts.SafeCancelAndDispose();
            popupCts.SafeCancelAndDispose();
        }

        private void OnRoomConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason = null)
        {
            if (lifeCts is { IsCancellationRequested: false })
                EvaluateOnceAsync(lifeCts.Token).Forget();
        }

        private async UniTaskVoid MonitorLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(POLL_INTERVAL_SECONDS), cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested)
                    return;

                await EvaluateOnceAsync(ct);
            }
        }

        private async UniTask EvaluateOnceAsync(CancellationToken ct)
        {
            // Single in-flight evaluation: events and the poll loop share this guard.
            if (evaluating)
                return;

            evaluating = true;

            try
            {
                if (IsMultiplayerConnected())
                {
                    everConnected = true;
                    DismissPopupIfShowing(); // recovered -> close the popup if it is up
                    return;
                }

                // Never connected yet -> start/teleport path, handled by the initialization flow.
                // Popup already up -> the user has been informed; don't re-probe or stack popups.
                if (!everConnected || popupShowing)
                    return;

                // Give the native reconnection loops a chance before escalating.
                await UniTask.Delay(TimeSpan.FromSeconds(SUSTAINED_LOSS_GRACE_SECONDS), cancellationToken: ct).SuppressCancellationThrow();

                if (ct.IsCancellationRequested || IsMultiplayerConnected())
                    return;

                if (await IsInternetUnreachableAsync(ct))
                    ShowConnectionLostPopupAsync(ct).Forget();

                // Reachable -> the problem is server-side, not the user's connection; let the reconnection loops keep working.
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportHub.LogException(exception, ReportCategory.MULTIPLAYER); }
            finally { evaluating = false; }
        }

        /// <summary>
        ///     Connected if EITHER transport is up; only when both are down do we suspect a full loss.
        ///     Pulse uses the raw socket state (not IsAuthenticated) so auth-level failures and the
        ///     reconnect handshake are not mistaken for a connectivity loss.
        /// </summary>
        private bool IsMultiplayerConnected() =>
            roomHub.HasAnyRoomConnected() || pulseTransport.State == ITransport.TransportState.CONNECTED;

        private async UniTask<bool> IsInternetUnreachableAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            // Option B — cheap OS gate: no route at all means definitely offline.
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return true;

            // Option A — confirm with a real probe to a highly-available endpoint.
            bool reachable = await webRequestController.IsHeadReachableAsync(
                ReportCategory.MULTIPLAYER, reachabilityProbeUrl, ct,
                timeout: PROBE_TIMEOUT_SECONDS, suppressErrors: true);

            return !reachable;
        }

        private async UniTaskVoid ShowConnectionLostPopupAsync(CancellationToken ct)
        {
            popupShowing = true;
            popupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                await UniTask.SwitchToMainThread();

                var input = new ErrorPopupWithRetryController.Input(
                    title: "Connection Lost",
                    description: "You appear to be offline. Please check your internet connection and retry.",
                    retryText: "Retry",
                    iconType: ErrorPopupWithRetryController.IconType.CONNECTION_LOST);

                // Returns when the user clicks Retry/Exit, or when DismissPopupIfShowing cancels popupCts on recovery.
                await mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(input), popupCts.Token);
            }
            catch (OperationCanceledException) { } // dismissed because connectivity recovered
            catch (Exception exception) { ReportHub.LogException(exception, ReportCategory.MULTIPLAYER); }
            finally
            {
                popupShowing = false;
                popupCts.SafeCancelAndDispose();
                popupCts = null;
            }
        }

        private void DismissPopupIfShowing()
        {
            if (popupShowing)
                popupCts.SafeCancelAndDispose();
        }
    }
}
