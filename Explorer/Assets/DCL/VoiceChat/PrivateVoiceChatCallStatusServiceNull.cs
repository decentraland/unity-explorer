using DCL.Utilities;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public class PrivateVoiceChatCallStatusServiceNull : IPrivateVoiceChatCallStatusService
    {
        public string CurrentTargetWallet { get; }
        public event Action<PrivateVoiceChatUpdate>? PrivateVoiceChatUpdateReceived;
        public IReadonlyReactiveProperty<VoiceChatStatus> Status { get; }
        public IReadonlyReactiveProperty<string> CallId { get; }
        public string ConnectionUrl { get; }

        public void StartCall(string target) { }

        public void HangUp() { }

        public void HandleLivekitConnectionFailed() { }

        public void HandleLivekitConnectionEnded() { }

        public void UpdateStatus(VoiceChatStatus newStatus) { }

        public void ResetVoiceChatData() { }

        public void SetCallId(string newCallId) { }

        public void Dispose() { }


        public void AcceptCall() { }

        public void RejectCall() { }

        public void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update) { }
    }
}
