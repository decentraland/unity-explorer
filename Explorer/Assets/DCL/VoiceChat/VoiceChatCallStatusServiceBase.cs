using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Abstract base class for voice chat call status services that provides common functionality
    ///     and enforces the contract for different voice chat implementations (private, community, etc.)
    /// </summary>
    public abstract class VoiceChatCallStatusServiceBase
    {
        private readonly ReactiveProperty<VoiceChatStatus> status = new (VoiceChatStatus.DISCONNECTED);

        public IReadonlyReactiveProperty<VoiceChatStatus> Status => status;
        public string CallId { get; protected set; }
        public string ConnectionUrl { get; protected set; }

        public abstract void StartCall(string target);

        public abstract void HangUp();

        public abstract void HandleLivekitConnectionFailed();

        protected void UpdateStatus(VoiceChatStatus newStatus)
        {
            UpdateStatusAsync().Forget();

            async UniTaskVoid UpdateStatusAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"New status is {newStatus}");
                status.Value = newStatus;
            }
        }

        protected void ResetVoiceChatData()
        {
            CallId = string.Empty;
            ConnectionUrl = string.Empty;
        }

        public virtual void Dispose()
        {
            status?.Dispose();
        }
    }
}
