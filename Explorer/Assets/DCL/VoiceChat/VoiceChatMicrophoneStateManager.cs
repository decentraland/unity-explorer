using DCL.Diagnostics;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneStateManager : IDisposable
    {
        private const string TAG = nameof(VoiceChatMicrophoneStateManager);

        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly IDisposable? statusSubscription;

        private VoiceChatStatus currentCallStatus;
        private bool isRoomConnected;
        private bool disposed;
        private bool microphoneEnabledForCurrentSession;

        public VoiceChatMicrophoneStateManager(
            VoiceChatMicrophoneHandler microphoneHandler,
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.microphoneHandler = microphoneHandler;

            statusSubscription = voiceChatOrchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            statusSubscription?.Dispose();
        }

        public void OnRoomConnectionChanged(bool connected)
        {
            if (isRoomConnected == connected) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Room connection changed: {isRoomConnected} -> {connected}");
            isRoomConnected = connected;

            UpdateMicrophoneState();
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == currentCallStatus) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Call status changed: {currentCallStatus} -> {newStatus}");
            currentCallStatus = newStatus;

            UpdateMicrophoneState();
        }

        private void UpdateMicrophoneState()
        {
            bool shouldEnableMicrophone = currentCallStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL && isRoomConnected;

            bool shouldDisableMicrophone = currentCallStatus == VoiceChatStatus.DISCONNECTED ||
                                           currentCallStatus == VoiceChatStatus.VOICE_CHAT_ENDING_CALL ||
                                           (!isRoomConnected && currentCallStatus != VoiceChatStatus.VOICE_CHAT_STARTING_CALL &&
                                            currentCallStatus != VoiceChatStatus.VOICE_CHAT_STARTED_CALL);

            // Only auto-enable the mic once per call session. This prevents re-enabling
            // the mic when a listener is promoted to speaker — promoted users should
            // join muted and explicitly unmute themselves.
            if (shouldEnableMicrophone && !microphoneEnabledForCurrentSession)
            {
                microphoneEnabledForCurrentSession = true;
                microphoneHandler.EnableMicrophoneForCall();
            }
            else if (shouldDisableMicrophone)
            {
                microphoneEnabledForCurrentSession = false;
                microphoneHandler.DisableMicrophoneForCall();
            }
        }
    }
}
