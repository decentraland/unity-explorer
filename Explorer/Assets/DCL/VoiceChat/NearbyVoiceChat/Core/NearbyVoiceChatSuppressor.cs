using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using System;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Drives Nearby state-machine transitions in response to external triggers:
    ///     higher-priority call status (Community/Private) and world loading stage.
    ///     Microphone-track lifecycle (publish, retry, device-switch, focus pause, reconnect, VAD) is owned by <see cref="NearbyMicrophoneHandler"/>.
    ///     Remote audio binding is owned by <see cref="DCL.VoiceChat.Nearby.Systems.NearbyAudioBindingSystem"/> driven from <see cref="DCL.VoiceChat.Nearby.Audio.INearbyAudioStreamRegistry"/>.
    /// </summary>
    public class NearbyVoiceChatSuppressor : IDisposable
    {
        private const string TAG = nameof(NearbyVoiceChatSuppressor);

        private readonly NearbyVoiceChatStateModel stateModel;

        private readonly IDisposable callStatusSubscription;
        private readonly IDisposable loadingStageSubscription;

        private bool disposed;

        public NearbyVoiceChatSuppressor(NearbyVoiceChatStateModel stateModel, IReadonlyReactiveProperty<VoiceChatStatus> callStatus, ILoadingStatus loadingStatus)
        {
            this.stateModel = stateModel;

            // Suppress while world is still loading so we do not attempt to connect before the player spawns.
            // User preference (DISABLED/IDLE from PlayerPrefs) is preserved as preBlockedState and restored on Resume(LOADING).
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                stateModel.Suppress(SuppressionReason.LOADING);

            loadingStageSubscription = loadingStatus.CurrentStage.Subscribe(OnLoadingStageChanged);
            callStatusSubscription = callStatus.Subscribe(OnCallStatusChanged);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            callStatusSubscription.Dispose();
            loadingStageSubscription.Dispose();
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status.IsInCall())
                stateModel.Suppress(SuppressionReason.CALL);
            else if (status.IsNotConnected())
                stateModel.Resume(SuppressionReason.CALL);
        }

        private void OnLoadingStageChanged(LoadingStatus.LoadingStage stage)
        {
            if (stage == LoadingStatus.LoadingStage.Completed)
                stateModel.Resume(SuppressionReason.LOADING);
            else
                stateModel.Suppress(SuppressionReason.LOADING);
        }
    }
}
