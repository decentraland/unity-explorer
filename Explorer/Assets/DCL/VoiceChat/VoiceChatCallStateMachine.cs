using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Infrastructure.StateMachine;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public enum VoiceChatCallState
    {
        DISCONNECTED,
        STARTING_CALL,
        STARTED_CALL,
        IN_CALL,
        ENDING_CALL,
        RECEIVED_CALL,
        REJECTING_CALL,
        USER_BUSY,
        ERROR
    }

    public class VoiceChatCallStateMachine : BaseStateMachine<VoiceChatCallState>, IVoiceChatCallStatusService, IDisposable
    {
        private readonly IVoiceService voiceChatService;
        private readonly ReactiveProperty<VoiceChatStatus> statusProperty;
        private CancellationTokenSource cts;
        private bool disposed;

        // Properties from original service
        public IReadonlyReactiveProperty<VoiceChatStatus> Status => statusProperty;
        public Web3Address CurrentTargetWallet { get; private set; }
        public string CallId { get; private set; }
        public string RoomUrl { get; private set; }

        public IReactiveProperty<bool> IsInCall { get; private set; }
        public IReactiveProperty<bool> IsCallActive { get; }
        public IReactiveProperty<bool> IsDisconnected { get; }
        public IReactiveProperty<bool> HasIncomingCall { get; }

        public VoiceChatCallStateMachine(IVoiceService voiceChatService)
            : base(VoiceChatCallState.DISCONNECTED)
        {
            this.voiceChatService = voiceChatService;
            this.statusProperty = new ReactiveProperty<VoiceChatStatus>(VoiceChatStatus.DISCONNECTED);
            this.cts = new CancellationTokenSource();

            // Initialize individual reactive properties
            this.IsInCall = new ReactiveProperty<bool>(false);
            this.IsCallActive = new ReactiveProperty<bool>(false);
            this.IsDisconnected = new ReactiveProperty<bool>(true);
            this.HasIncomingCall = new ReactiveProperty<bool>(false);

            // Subscribe to voice service events
            this.voiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            this.voiceChatService.Reconnected += OnReconnected;
            this.voiceChatService.Disconnected += OnRCPDisconnected;
        }

        protected override void InitializeStateMachine()
        {
            // Define all valid transitions
            DefineTransition(VoiceChatCallState.DISCONNECTED, VoiceChatCallState.STARTING_CALL);
            DefineTransition(VoiceChatCallState.DISCONNECTED, VoiceChatCallState.RECEIVED_CALL);

            DefineTransition(VoiceChatCallState.STARTING_CALL, VoiceChatCallState.STARTED_CALL);
            DefineTransition(VoiceChatCallState.STARTING_CALL, VoiceChatCallState.USER_BUSY);
            DefineTransition(VoiceChatCallState.STARTING_CALL, VoiceChatCallState.ERROR);
            DefineTransition(VoiceChatCallState.STARTING_CALL, VoiceChatCallState.DISCONNECTED);

            DefineTransition(VoiceChatCallState.STARTED_CALL, VoiceChatCallState.IN_CALL);
            DefineTransition(VoiceChatCallState.STARTED_CALL, VoiceChatCallState.ERROR);
            DefineTransition(VoiceChatCallState.STARTED_CALL, VoiceChatCallState.DISCONNECTED);

            DefineTransition(VoiceChatCallState.IN_CALL, VoiceChatCallState.ENDING_CALL);
            DefineTransition(VoiceChatCallState.IN_CALL, VoiceChatCallState.ERROR);
            DefineTransition(VoiceChatCallState.IN_CALL, VoiceChatCallState.DISCONNECTED);

            DefineTransition(VoiceChatCallState.ENDING_CALL, VoiceChatCallState.DISCONNECTED);
            DefineTransition(VoiceChatCallState.ENDING_CALL, VoiceChatCallState.ERROR);

            DefineTransition(VoiceChatCallState.RECEIVED_CALL, VoiceChatCallState.STARTED_CALL);
            DefineTransition(VoiceChatCallState.RECEIVED_CALL, VoiceChatCallState.REJECTING_CALL);
            DefineTransition(VoiceChatCallState.RECEIVED_CALL, VoiceChatCallState.DISCONNECTED);

            DefineTransition(VoiceChatCallState.REJECTING_CALL, VoiceChatCallState.DISCONNECTED);
            DefineTransition(VoiceChatCallState.REJECTING_CALL, VoiceChatCallState.ERROR);

            DefineTransition(VoiceChatCallState.USER_BUSY, VoiceChatCallState.DISCONNECTED);
            DefineTransition(VoiceChatCallState.USER_BUSY, VoiceChatCallState.STARTING_CALL);

            DefineTransition(VoiceChatCallState.ERROR, VoiceChatCallState.DISCONNECTED);
            DefineTransition(VoiceChatCallState.ERROR, VoiceChatCallState.STARTING_CALL);

            // Define state enter actions
            DefineStateEnterAction(VoiceChatCallState.DISCONNECTED, OnEnterDisconnected);
            DefineStateEnterAction(VoiceChatCallState.STARTING_CALL, OnEnterStartingCall);
            DefineStateEnterAction(VoiceChatCallState.STARTED_CALL, OnEnterStartedCall);
            DefineStateEnterAction(VoiceChatCallState.IN_CALL, OnEnterInCall);
            DefineStateEnterAction(VoiceChatCallState.ENDING_CALL, OnEnterEndingCall);
            DefineStateEnterAction(VoiceChatCallState.RECEIVED_CALL, OnEnterReceivedCall);
            DefineStateEnterAction(VoiceChatCallState.REJECTING_CALL, OnEnterRejectingCall);
            DefineStateEnterAction(VoiceChatCallState.USER_BUSY, OnEnterUserBusy);
            DefineStateEnterAction(VoiceChatCallState.ERROR, OnEnterError);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (voiceChatService != null)
            {
                voiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
                voiceChatService.Reconnected -= OnReconnected;
                voiceChatService.Disconnected -= OnRCPDisconnected;
                voiceChatService.Dispose();
            }

            statusProperty.Dispose();
            IsInCall.Dispose();
            IsCallActive.Dispose();
            IsDisconnected.Dispose();
            HasIncomingCall.Dispose();
            cts.SafeCancelAndDispose();
        }

        // Public API methods from IVoiceChatCallStatusService
        public void StartCall(Web3Address walletId)
        {
            CurrentTargetWallet = walletId;
            cts = cts.SafeRestart();

            // Let the state machine handle the transition
            if (TransitionTo(VoiceChatCallState.STARTING_CALL))
            {
                StartCallAsync(walletId, cts.Token).Forget();
            }
        }

        public void AcceptCall()
        {
            cts = cts.SafeRestart();

            // Let the state machine handle the transition
            if (TransitionTo(VoiceChatCallState.STARTED_CALL))
            {
                AcceptCallAsync(CallId, cts.Token).Forget();
            }
        }

        public void HangUp()
        {
            cts = cts.SafeRestart();

            // Let the state machine handle the transition
            if (TransitionTo(VoiceChatCallState.ENDING_CALL))
            {
                HangUpAsync(CallId, cts.Token).Forget();
            }
        }

        public void RejectCall()
        {
            cts = cts.SafeRestart();

            // Let the state machine handle the transition
            if (TransitionTo(VoiceChatCallState.REJECTING_CALL))
            {
                RejectCallAsync(CallId, cts.Token).Forget();
            }
        }

        public void HandleLivekitConnectionFailed()
        {
            if (IsCallActive.Value)
            {
                ResetVoiceChatData();
                HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }

        // Event handlers from original service
        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            switch (update.Status)
            {
                case PrivateVoiceChatStatus.VoiceChatAccepted:
                    RoomUrl = update.Credentials.ConnectionUrl;
                    HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                    break;
                case PrivateVoiceChatStatus.VoiceChatEnded:
                    ResetVoiceChatData();
                    HandleExternalStatusChange(VoiceChatStatus.DISCONNECTED);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRejected:
                    ResetVoiceChatData();
                    HandleExternalStatusChange(VoiceChatStatus.DISCONNECTED);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRequested:
                    CallId = update.CallId;
                    CurrentTargetWallet = new Web3Address(update.Caller.Address);
                    HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
                    break;
                case PrivateVoiceChatStatus.VoiceChatExpired:
                    ResetVoiceChatData();
                    HandleExternalStatusChange(VoiceChatStatus.DISCONNECTED);
                    break;
            }
        }

        private void OnReconnected()
        {
            CheckIncomingCallAsync(cts.Token).Forget();
        }

        private void OnRCPDisconnected()
        {
            if (!IsInCall.Value)
            {
                ResetVoiceChatData();
                HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }

        // Async methods from original service
        private async UniTaskVoid CheckIncomingCallAsync(CancellationToken ct)
        {
            try
            {
                GetIncomingPrivateVoiceChatRequestResponse response = await voiceChatService.GetIncomingPrivateVoiceChatRequestAsync(ct);

                if (response.ResponseCase == GetIncomingPrivateVoiceChatRequestResponse.ResponseOneofCase.Ok)
                {
                    CallId = response.Ok.CallId;
                    HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
                }
            }
            catch (Exception e)
            {
                HandleVoiceChatServiceDisabled(e, resetData: false);
            }
        }

        private async UniTaskVoid StartCallAsync(Web3Address walletId, CancellationToken ct)
        {
            try
            {
                StartPrivateVoiceChatResponse response = await voiceChatService.StartPrivateVoiceChatAsync(walletId.ToString(), ct);

                switch (response.ResponseCase)
                {
                    case StartPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                        CallId = response.Ok.CallId;
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
                        break;
                    case StartPrivateVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                    case StartPrivateVoiceChatResponse.ResponseOneofCase.ConflictingError:
                        ResetVoiceChatData();
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_USER_BUSY);
                        break;
                    default:
                        ResetVoiceChatData();
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
                HandleVoiceChatServiceDisabled(e, resetData: true);
            }
        }

        private async UniTaskVoid AcceptCallAsync(string callId, CancellationToken ct)
        {
            try
            {
                AcceptPrivateVoiceChatResponse response = await voiceChatService.AcceptPrivateVoiceChatAsync(callId, ct);

                switch (response.ResponseCase)
                {
                    case AcceptPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                        RoomUrl = response.Ok.Credentials.ConnectionUrl;
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;
                    default:
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
                HandleVoiceChatServiceDisabled(e, resetData: false);
            }
        }

        private async UniTaskVoid HangUpAsync(string callId, CancellationToken ct)
        {
            try
            {
                EndPrivateVoiceChatResponse response = await voiceChatService.EndPrivateVoiceChatAsync(callId, ct);

                switch (response.ResponseCase)
                {
                    case EndPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                        ResetVoiceChatData();
                        HandleExternalStatusChange(VoiceChatStatus.DISCONNECTED);
                        break;
                    default:
                        ResetVoiceChatData();
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
                HandleVoiceChatServiceDisabled(e, resetData: true);
            }
        }

        private async UniTaskVoid RejectCallAsync(string callId, CancellationToken ct)
        {
            try
            {
                RejectPrivateVoiceChatResponse response = await voiceChatService.RejectPrivateVoiceChatAsync(callId, ct);

                switch (response.ResponseCase)
                {
                    case RejectPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                        HandleExternalStatusChange(VoiceChatStatus.DISCONNECTED);
                        break;
                    default:
                        HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
                HandleVoiceChatServiceDisabled(e, resetData: false);
            }
        }

        // Helper methods
        private void HandleExternalStatusChange(VoiceChatStatus newStatus)
        {
            var targetState = MapVoiceChatStatusToState(newStatus);
            TransitionTo(targetState);
        }

        private VoiceChatCallState MapVoiceChatStatusToState(VoiceChatStatus status)
        {
            return status switch
            {
                VoiceChatStatus.DISCONNECTED => VoiceChatCallState.DISCONNECTED,
                VoiceChatStatus.VOICE_CHAT_STARTING_CALL => VoiceChatCallState.STARTING_CALL,
                VoiceChatStatus.VOICE_CHAT_STARTED_CALL => VoiceChatCallState.STARTED_CALL,
                VoiceChatStatus.VOICE_CHAT_IN_CALL => VoiceChatCallState.IN_CALL,
                VoiceChatStatus.VOICE_CHAT_ENDING_CALL => VoiceChatCallState.ENDING_CALL,
                VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL => VoiceChatCallState.RECEIVED_CALL,
                VoiceChatStatus.VOICE_CHAT_REJECTING_CALL => VoiceChatCallState.REJECTING_CALL,
                VoiceChatStatus.VOICE_CHAT_USER_BUSY => VoiceChatCallState.USER_BUSY,
                VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR => VoiceChatCallState.ERROR,
                _ => VoiceChatCallState.ERROR
            };
        }

        private void ResetVoiceChatData()
        {
            CallId = string.Empty;
            RoomUrl = string.Empty;
        }

        private void HandleVoiceChatServiceDisabled(Exception e, bool resetData = false)
        {
            ReportHub.LogWarning($"Voice chat service is disabled: {e.Message}", new ReportData(ReportCategory.VOICE_CHAT));
            if (resetData)
            {
                ResetVoiceChatData();
            }
            HandleExternalStatusChange(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        // State enter actions - these trigger the status updates
        private void OnEnterDisconnected()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered DISCONNECTED state");
            statusProperty.Value = VoiceChatStatus.DISCONNECTED;
            IsDisconnected.Value = true;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterStartingCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered STARTING_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_STARTING_CALL;
            IsDisconnected.Value = false;
            IsInCall.UpdateValue(false);
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterStartedCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered STARTED_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_STARTED_CALL;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterInCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered IN_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_IN_CALL;
            IsDisconnected.Value = false;
            IsInCall.Value = true;
            IsCallActive.Value = true;
            HasIncomingCall.Value = false;
        }

        private void OnEnterEndingCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered ENDING_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_ENDING_CALL;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterReceivedCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered RECEIVED_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = true;
        }

        private void OnEnterRejectingCall()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered REJECTING_CALL state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_REJECTING_CALL;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterUserBusy()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered USER_BUSY state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_USER_BUSY;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        private void OnEnterError()
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, "Entered ERROR state");
            statusProperty.Value = VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR;
            IsDisconnected.Value = false;
            IsInCall.Value = false;
            IsCallActive.Value = false;
            HasIncomingCall.Value = false;
        }

        // Null instance for testing
        public static class Null
        {
            public static readonly VoiceChatCallStateMachine INSTANCE = new(IVoiceService.Null.INSTANCE);
        }
    }
}
