using DCL.Web3;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public interface IVoiceChatCallStatusService : IDisposable
    {
        IReadonlyReactiveProperty<VoiceChatStatus> Status { get; }
        public Web3Address CurrentTargetWallet { get; }
        public string RoomUrl { get;}

        void StartCall(Web3Address userAddress);
        void AcceptCall();
        void HangUp();
        void RejectCall();
        void HandleLivekitConnectionFailed();
    }
}
