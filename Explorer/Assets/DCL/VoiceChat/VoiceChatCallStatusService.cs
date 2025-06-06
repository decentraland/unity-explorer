using DCL.Web3;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatCallStatusService : IVoiceChatCallStatusService
    {
        public event IVoiceChatCallStatusService.VoiceChatStatusChangeDelegate StatusChanged;
        public VoiceChatStatus Status { get; private set; }
        public Web3Address CurrentTargetWallet { get; private set; }

        public VoiceChatCallStatusService()
        {

        }

        public void StartCall(Web3Address walletId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status is not VoiceChatStatus.DISCONNECTED) return;

            CurrentTargetWallet = walletId;
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);
        }

        public void StopCall()
        {
            //We can stop a call only if we are starting a call or inside a call
            if (Status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDED_CALL or VoiceChatStatus.VOICE_CHAT_ENDING_CALL) return;

            UpdateStatus(VoiceChatStatus.DISCONNECTED);
        }

        private void UpdateStatus(VoiceChatStatus newStatus)
        {
            Status = newStatus;
            StatusChanged?.Invoke(Status, CurrentTargetWallet);
        }
    }
}
