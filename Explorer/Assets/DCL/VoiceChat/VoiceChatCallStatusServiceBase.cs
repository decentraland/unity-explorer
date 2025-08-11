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
        private const string TAG = nameof(VoiceChatCallStatusServiceBase);

        private readonly ReactiveProperty<VoiceChatStatus> status = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<string> callId = new (string.Empty);

        public IReadonlyReactiveProperty<VoiceChatStatus> Status => status;
        public IReadonlyReactiveProperty<string> CallId => callId;
        public string ConnectionUrl { get; protected set; }

        public abstract void StartCall(string target);

        public abstract void HangUp();

        public abstract void HandleLivekitConnectionFailed();

        public abstract void HandleLivekitConnectionEnded();

        protected void UpdateStatus(VoiceChatStatus newStatus)
        {
            UpdateStatusAsync().Forget();

            async UniTaskVoid UpdateStatusAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New status is {newStatus}");
                status.Value = newStatus;
            }
        }

        protected void ResetVoiceChatData()
        {
            callId.Value = string.Empty;
            ConnectionUrl = string.Empty;
        }

        protected void SetCallId(string newCallId)
        {
            callId.Value = newCallId;
        }

        public virtual void Dispose()
        {
            status?.Dispose();
            callId?.Dispose();
        }
    }
}
