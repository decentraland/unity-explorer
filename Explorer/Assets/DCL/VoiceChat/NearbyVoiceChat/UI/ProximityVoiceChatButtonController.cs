using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using System;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatButtonController : IDisposable
    {
        private const string CALL_SUPPRESSED_TEXT = "Nearby voice chat unavailable\nduring Calls & Streams.";
        private const string SCENE_SUPPRESSED_TEXT = "Nearby voice chat unavailable\nin this scene.";

        private readonly ProximityVoiceChatButtonView view;
        private readonly ReactivePropertyExtensions.DisposableSubscription<NearbyVoiceChatState> stateSubscription;
        private readonly ReactivePropertyExtensions.DisposableSubscription<string?> suppressionSubscription;

        public ProximityVoiceChatButtonController(
            ProximityVoiceChatButtonView view,
            NearbyVoiceChatStateModel stateModel)
        {
            this.view = view;

            view.SetState(stateModel.State.Value);
            view.IsBlocked = stateModel.State.Value == NearbyVoiceChatState.SUPPRESSED;
            view.CloseAreaButton.onClick.AddListener(view.HideDisabledTooltip);
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            suppressionSubscription = stateModel.ActiveSuppression.Subscribe(OnSuppressionReasonChanged);
        }

        private void OnStateChanged(NearbyVoiceChatState state)
        {
            view.SetState(state);
            view.IsBlocked = state == NearbyVoiceChatState.SUPPRESSED;

            if (state != NearbyVoiceChatState.SUPPRESSED)
                view.HideDisabledTooltip();
        }

        private void OnSuppressionReasonChanged(string? reason)
        {
            if (reason == null) return;

            string text = reason == NearbyVoiceChatStateModel.SUPPRESSION_SCENE
                ? SCENE_SUPPRESSED_TEXT
                : CALL_SUPPRESSED_TEXT;

            view.SuppressedText.text = text;
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            suppressionSubscription.Dispose();
            view.CloseAreaButton.onClick.RemoveListener(view.HideDisabledTooltip);
            view.HideDisabledTooltip();
        }
    }
}
