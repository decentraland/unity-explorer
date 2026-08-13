using Cysharp.Threading.Tasks;
using DCL.ChatArea;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Prefs;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat.UI
{
    /// <summary>
    ///     Drives the Nearby Voice Chat intro tip: schedules it by launch count, caps how many times it is ever
    ///     displayed, retires it once the user actually speaks over nearby voice, and hides it while a panel covers
    ///     the screen. Usage is recorded even when the tip is not scheduled, so enabling the feature flag later does
    ///     not resurface the tip for users who already found the feature on their own.
    /// </summary>
    public class NearbyVoiceTipController : IDisposable
    {
        private readonly NearbyVoiceTipView view;
        private readonly NearbyVoiceChatButtonView nearbyVoiceChatButtonView;
        private readonly EventSubscriptionScope scope = new ();
        private readonly ReactivePropertyExtensions.DisposableSubscription<NearbyVoiceChatState> stateSubscription;

        private CancellationTokenSource? cts;
        private bool hasUsedNearbyVoice;
        private bool isTipLive;
        private int coveringViews;
        private int timesShown;

        public NearbyVoiceTipController(
            NearbyVoiceTipView view,
            NearbyVoiceChatButtonView nearbyVoiceChatButtonView,
            NearbyVoiceChatStateModel stateModel,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            ILoadingStatus loadingStatus,
            NearbyVoiceTipSchedule schedule)
        {
            this.view = view;
            this.nearbyVoiceChatButtonView = nearbyVoiceChatButtonView;

            hasUsedNearbyVoice = DCLPlayerPrefs.GetBool(DCLPrefKeys.NEARBY_VOICE_USED);
            stateSubscription = stateModel.State.Subscribe(OnNearbyVoiceStateChanged);

            view.Hide();

            if (!IsDue(schedule)) return;

            scope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.MVCViewOpenEvent>(OnMVCViewOpened));
            scope.Add(chatSharedAreaEventBus.Subscribe<ChatSharedAreaEvents.MVCViewClosedEvent>(OnMVCViewClosed));

            cts = new CancellationTokenSource();
            ShowWhenReadyAsync(loadingStatus, cts.Token).Forget();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            cts = null;
            stateSubscription.Dispose();
            scope.Dispose();
        }

        private bool IsDue(NearbyVoiceTipSchedule schedule)
        {
            // Users who dismissed the tip back when it was shown once on first login are never shown it again.
            if (DCLPlayerPrefs.GetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED)) return false;

            timesShown = DCLPlayerPrefs.GetInt(DCLPrefKeys.NEARBY_VOICE_TIP_SHOWN_COUNT);
            int lastShownAtLaunch = DCLPlayerPrefs.GetInt(DCLPrefKeys.NEARBY_VOICE_TIP_LAST_SHOWN_LAUNCH);

            return schedule.ShouldShow(LaunchCounter.Count, timesShown, lastShownAtLaunch, hasUsedNearbyVoice);
        }

        private async UniTaskVoid ShowWhenReadyAsync(ILoadingStatus loadingStatus, CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(
                    () => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed && coveringViews == 0,
                    cancellationToken: ct);

                // Count the display up front: a user who quits without pressing either button has still seen the tip.
                DCLPlayerPrefs.SetInt(DCLPrefKeys.NEARBY_VOICE_TIP_SHOWN_COUNT, timesShown + 1);
                DCLPlayerPrefs.SetInt(DCLPrefKeys.NEARBY_VOICE_TIP_LAST_SHOWN_LAUNCH, LaunchCounter.Count, save: true);

                isTipLive = true;
                view.Show();

                int winner = await UniTask.WhenAny(
                    view.CloseButton.OnClickAsync(ct),
                    view.TryItNowButton.OnClickAsync(ct));

                Retire();

                if (winner == 1)
                    nearbyVoiceChatButtonView.Button.onClick.Invoke();
            }
            catch (OperationCanceledException) { }
        }

        private void OnNearbyVoiceStateChanged(NearbyVoiceChatState state)
        {
            if (hasUsedNearbyVoice || state != NearbyVoiceChatState.OpenMic) return;

            hasUsedNearbyVoice = true;
            DCLPlayerPrefs.SetBool(DCLPrefKeys.NEARBY_VOICE_USED, true, save: true);

            if (isTipLive)
                Retire();
        }

        private void OnMVCViewOpened(ChatSharedAreaEvents.MVCViewOpenEvent evt)
        {
            if (!IsCoveringLayer(evt.ViewSortingLayer)) return;

            coveringViews++;

            if (isTipLive)
                view.Hide();
        }

        private void OnMVCViewClosed(ChatSharedAreaEvents.MVCViewClosedEvent evt)
        {
            if (!IsCoveringLayer(evt.ViewSortingLayer)) return;

            coveringViews = Math.Max(0, coveringViews - 1);

            if (coveringViews == 0 && isTipLive)
                view.Show();
        }

        private static bool IsCoveringLayer(CanvasOrdering.SortingLayer layer) =>
            layer is CanvasOrdering.SortingLayer.Fullscreen or CanvasOrdering.SortingLayer.Popup;

        private void Retire()
        {
            isTipLive = false;
            view.Hide();
            cts.SafeCancelAndDispose();
            cts = null;
        }
    }
}