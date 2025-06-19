using DCL.Diagnostics;
using System;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatMicrophoneStateManager : IDisposable
    {
        private readonly VoiceChatMicrophoneHandler microphoneHandler;
        private readonly IVoiceChatCallStatusService voiceChatCallStatusService;

        private VoiceChatStatus currentCallStatus;
        private bool isRoomConnected;
        private bool disposed;

        public VoiceChatMicrophoneStateManager(
            VoiceChatMicrophoneHandler microphoneHandler,
            IVoiceChatCallStatusService voiceChatCallStatusService)
        {
            this.microphoneHandler = microphoneHandler;
            this.voiceChatCallStatusService = voiceChatCallStatusService;

            voiceChatCallStatusService.StatusChanged += OnCallStatusChanged;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            voiceChatCallStatusService.StatusChanged -= OnCallStatusChanged;
        }

        public void OnRoomConnectionChanged(bool connected)
        {
            if (isRoomConnected == connected) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Room connection changed: {isRoomConnected} -> {connected}");
            isRoomConnected = connected;
            
            if (connected)
            {
                microphoneHandler.Reset();
            }
            
            UpdateMicrophoneState();
        }

        private void OnCallStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == currentCallStatus) return;

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Call status changed: {currentCallStatus} -> {newStatus}");
            currentCallStatus = newStatus;

            UpdateMicrophoneState();
        }

        private void UpdateMicrophoneState()
        {
            bool shouldEnableMicrophone = currentCallStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL && isRoomConnected;
            bool shouldDisableMicrophone = currentCallStatus == VoiceChatStatus.DISCONNECTED || 
                                         (currentCallStatus == VoiceChatStatus.VOICE_CHAT_ENDING_CALL) ||
                                         (!isRoomConnected && currentCallStatus != VoiceChatStatus.VOICE_CHAT_STARTING_CALL && 
                                          currentCallStatus != VoiceChatStatus.VOICE_CHAT_STARTED_CALL);

            if (shouldEnableMicrophone)
            {
                microphoneHandler.EnableMicrophoneForCall();
            }
            else if (shouldDisableMicrophone)
            {
                microphoneHandler.DisableMicrophoneForCall();
            }
        }
    }
}
