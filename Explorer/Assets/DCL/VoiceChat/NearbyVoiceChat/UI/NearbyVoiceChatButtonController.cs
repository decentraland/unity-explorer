using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using System;

namespace DCL.VoiceChat.UI
{
    public class NearbyVoiceChatButtonController : IDisposable
    {
        private const string CALL_SUPPRESSED_TEXT = "Nearby voice chat unavailable\nduring Calls & Streams.";
        private const string SCENE_SUPPRESSED_TEXT = "Nearby voice chat unavailable\nin this scene.";
        private const string SCENE_BAN_SUPPRESSED_TEXT = "Nearby voice chat unavailable\nyou are banned from this scene.";

        private readonly NearbyVoiceChatButtonView view;
        private readonly ReactivePropertyExtensions.DisposableSubscription<NearbyVoiceChatState> stateSubscription;
        private readonly ReactivePropertyExtensions.DisposableSubscription<SuppressionReason?> suppressionSubscription;

        public NearbyVoiceChatButtonController(NearbyVoiceChatButtonView view, NearbyVoiceChatStateModel stateModel)
        {
            this.view = view;

            view.SetState(stateModel.State.Value);
            view.IsSuppressed = stateModel.State.Value == NearbyVoiceChatState.SUPPRESSED;
            view.CloseAreaButton.onClick.AddListener(view.HideDisabledTooltip);
            view.InitializeSoundWave(() => stateModel.IsLocalSpeaking ? 1f : 0f);
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            suppressionSubscription = stateModel.ActiveSuppression.Subscribe(OnSuppressionReasonChanged);
        }

        private void OnStateChanged(NearbyVoiceChatState state)
        {
            view.SetState(state);
            view.IsSuppressed = state == NearbyVoiceChatState.SUPPRESSED;

            if (state != NearbyVoiceChatState.SUPPRESSED)
                view.HideDisabledTooltip();
        }

        private void OnSuppressionReasonChanged(SuppressionReason? reason)
        {
            if (reason == null) return;

            // LOADING falls through to CALL text — historical behavior preserved; users see a generic
            // "unavailable" hint during the transient startup window.
            string text = reason switch
            {
                SuppressionReason.SCENE => SCENE_SUPPRESSED_TEXT,
                SuppressionReason.SCENE_BAN => SCENE_BAN_SUPPRESSED_TEXT,
                _ => CALL_SUPPRESSED_TEXT,
            };

            view.SuppressedText.text = text;
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            suppressionSubscription.Dispose();
            view.CloseAreaButton.onClick.RemoveListener(view.HideDisabledTooltip);
        }
    }
}
