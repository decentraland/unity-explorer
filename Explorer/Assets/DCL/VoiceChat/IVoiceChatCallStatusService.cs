using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatCallStatusService
    {
        event Action<VoiceChatStatus> StatusChanged;
        VoiceChatStatus Status { get; }

        void StartCall(string walletId);
        void StopCall();
    }
}
