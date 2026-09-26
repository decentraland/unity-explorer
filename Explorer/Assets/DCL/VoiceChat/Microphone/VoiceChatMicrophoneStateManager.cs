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

        public void OnRoomConnectionChangedMuted(bool connected)
        {
            if (isRoomConnected == connected) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Room connection changed (muted): {isRoomConnected} -> {connected}");
            isRoomConnected = connected;

            if (!connected)
                UpdateMicrophoneState();
            else
                microphoneHandler.DisableMicrophoneForCall();
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
            bool shouldEnableMicrophone = currentCallStatus == VoiceChatStatus.VoiceChatInCall && isRoomConnected;

            bool shouldDisableMicrophone = currentCallStatus == VoiceChatStatus.Disconnected ||
                                           currentCallStatus == VoiceChatStatus.VoiceChatEndingCall ||
                                           (!isRoomConnected && currentCallStatus != VoiceChatStatus.VoiceChatStartingCall &&
                                            currentCallStatus != VoiceChatStatus.VoiceChatStartedCall);

            if (shouldEnableMicrophone) { microphoneHandler.EnableMicrophoneForCall(); }
            else if (shouldDisableMicrophone) { microphoneHandler.DisableMicrophoneForCall(); }
        }
    }
}
