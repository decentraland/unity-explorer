using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Abstract base class for voice chat call status services that provides common functionality
    /// and enforces the contract for different voice chat implementations (private, community, etc.)
    /// </summary>
    public abstract class VoiceChatCallStatusServiceBase
    {
        protected readonly ReactiveProperty<VoiceChatStatus> statusProperty = new (VoiceChatStatus.DISCONNECTED);

        public IReadonlyReactiveProperty<VoiceChatStatus> Status => statusProperty;
        public string CallId { get; protected set; }
        public string RoomUrl { get; protected set; }

        public abstract void StartCall(string target);
        public abstract void HangUp();
        public abstract void HandleLivekitConnectionFailed();

        protected void UpdateStatus(VoiceChatStatus newStatus)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"New status is {newStatus}");
            statusProperty.Value = newStatus;
        }

        protected void ResetVoiceChatData()
        {
            CallId = string.Empty;
            RoomUrl = string.Empty;
        }

        public virtual void Dispose()
        {
            statusProperty?.Dispose();
        }
    }
}
