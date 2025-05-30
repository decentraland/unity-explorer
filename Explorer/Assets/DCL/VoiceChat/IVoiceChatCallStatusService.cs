using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatCallStatusService
    {
        public delegate void VoiceChatStatusChangeDelegate(VoiceChatStatus newStatus);
        event VoiceChatStatusChangeDelegate StatusChanged;
        VoiceChatStatus Status { get; }

        void StartCall(string walletId);
        void StopCall();
    }
}
