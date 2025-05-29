using DCL.Web3;
using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatCallStatusService
    {
        event Action<VoiceChatStatus, Web3Address> StatusChanged;
        VoiceChatStatus Status { get; }
        public Web3Address CurrentTargetWallet { get; }

        void StartCall(Web3Address userAddress);
        void StopCall();
    }
}
