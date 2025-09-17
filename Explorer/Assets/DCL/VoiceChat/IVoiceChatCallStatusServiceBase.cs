using DCL.Utilities;

namespace DCL.VoiceChat
{
    public interface IVoiceChatCallStatusServiceBase
    {
        IReadonlyReactiveProperty<VoiceChatStatus> Status { get; }
        IReadonlyReactiveProperty<string> CallId { get; }
        string ConnectionUrl { get; }

        void StartCall(string target);

        void HangUp();

        void HandleLivekitConnectionFailed();

        void HandleLivekitConnectionEnded();

        public void UpdateStatus(VoiceChatStatus newStatus);

        void ResetVoiceChatData();

        void SetCallId(string newCallId);

        public void Dispose();
    }
}
