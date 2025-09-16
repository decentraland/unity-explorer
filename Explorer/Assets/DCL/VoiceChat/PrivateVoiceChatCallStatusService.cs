using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    ///     Implementation of voice chat call status service for private calls.
    ///     Handles all private voice chat operations and state management.
    /// </summary>
    public class PrivateVoiceChatCallStatusService : IPrivateVoiceChatCallStatusService
    {
        private const string TAG = nameof(PrivateVoiceChatCallStatusService);
        public IReadonlyReactiveProperty<VoiceChatStatus> Status => status;
        IReadonlyReactiveProperty<string> IVoiceChatCallStatusServiceBase.CallId => callId1;
        string IVoiceChatCallStatusServiceBase.ConnectionUrl => connectionUrl;

        private readonly IVoiceService voiceChatService;
        private CancellationTokenSource cts;
        private readonly ReactiveProperty<VoiceChatStatus> status = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<string> callId1 = new (string.Empty);
        private string connectionUrl = string.Empty;

        public string CurrentTargetWallet { get; private set; } = string.Empty;

        public event Action<PrivateVoiceChatUpdate>? PrivateVoiceChatUpdateReceived;

        public PrivateVoiceChatCallStatusService(IVoiceService voiceChatService)
        {
            this.voiceChatService = voiceChatService;

            this.voiceChatService.Reconnected += OnReconnected;
            this.voiceChatService.Disconnected += OnRCPDisconnected;
            this.voiceChatService.PrivateVoiceChatUpdateReceived += OnRPCPrivateVoiceChatUpdateReceived;
            cts = new CancellationTokenSource();
        }

        public void SetCallId(string newCallId)
        {
            callId1.Value = newCallId;
        }

        public void Dispose()
        {
            voiceChatService.Reconnected -= OnReconnected;
            voiceChatService.Disconnected -= OnRCPDisconnected;
            voiceChatService.PrivateVoiceChatUpdateReceived -= OnRPCPrivateVoiceChatUpdateReceived;

            cts.SafeCancelAndDispose();
        }

        private void OnRPCPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            PrivateVoiceChatUpdateReceived?.Invoke(update);
        }

        public void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            switch (update.Status)
            {
                case PrivateVoiceChatStatus.VoiceChatAccepted:
                    connectionUrl = update.Credentials.ConnectionUrl;
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRejected:
                case PrivateVoiceChatStatus.VoiceChatEnded:
                case PrivateVoiceChatStatus.VoiceChatExpired:
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.DISCONNECTED);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRequested:
                    SetCallId(update.CallId);
                    CurrentTargetWallet = new Web3Address(update.Caller.Address);
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
                    break;
            }
        }

        private void OnReconnected()
        {
            CheckIncomingCallAsync(cts.Token).Forget();
        }

        private async UniTaskVoid CheckIncomingCallAsync(CancellationToken ct)
        {
            try
            {
                GetIncomingPrivateVoiceChatRequestResponse response = await voiceChatService.GetIncomingPrivateVoiceChatRequestAsync(ct);

                if (response.ResponseCase == GetIncomingPrivateVoiceChatRequestResponse.ResponseOneofCase.Ok)
                {
                    SetCallId(response.Ok.CallId);
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
                }
            }
            catch (Exception e) { HandleVoiceChatServiceDisabled(e, resetData: false); }
        }

        private void OnRCPDisconnected()
        {
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }



        public void StartCall(string walletId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (!status.Value.IsNotConnected()) return;

            CurrentTargetWallet = walletId;

            cts = cts.SafeRestart();

            //Setting starting call status to instantly disable call button
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            StartCallAsync(cts.Token).Forget();
            return;

            async UniTaskVoid StartCallAsync(CancellationToken ct)
            {
                try
                {
                    StartPrivateVoiceChatResponse response = await voiceChatService.StartPrivateVoiceChatAsync(walletId, ct);

                    switch (response.ResponseCase)
                    {
                        //When the call can be started
                        case StartPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                            SetCallId(response.Ok.CallId);
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
                            break;

                        //When the other user is already in a call or is already being called
                        case StartPrivateVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                        case StartPrivateVoiceChatResponse.ResponseOneofCase.ConflictingError:
                            ResetVoiceChatData();
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_BUSY);
                            break;
                        default:
                            ResetVoiceChatData();
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                            break;
                    }
                }
                catch (Exception e) { HandleVoiceChatServiceDisabled(e, resetData: true); }
            }
        }

        public void AcceptCall()
        {
            //We can accept a call only if we are receiving a call
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);

            AcceptCallAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid AcceptCallAsync(string callId, CancellationToken ct)
            {
                try
                {
                    AcceptPrivateVoiceChatResponse response = await voiceChatService.AcceptPrivateVoiceChatAsync(callId, ct);

                    switch (response.ResponseCase)
                    {
                        //When the call has been ended
                        case AcceptPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                            connectionUrl = response.Ok.Credentials.ConnectionUrl;
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                            break;
                        default:
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                            break;
                    }
                }
                catch (Exception e) { HandleVoiceChatServiceDisabled(e, resetData: false); }
            }
        }

        public void HangUp()
        {
            //We can stop a call only if we are starting a call or inside a call
            if (status.Value is not (VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL or VoiceChatStatus.VOICE_CHAT_IN_CALL)) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_ENDING_CALL);
            HangUpAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid HangUpAsync(string callId, CancellationToken ct)
            {
                try
                {
                    EndPrivateVoiceChatResponse response = await voiceChatService.EndPrivateVoiceChatAsync(callId, ct);

                    switch (response.ResponseCase)
                    {
                        //When the call has been ended
                        case EndPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                            ResetVoiceChatData();
                            UpdateStatus(VoiceChatStatus.DISCONNECTED);
                            break;
                        default:
                            ResetVoiceChatData();
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                            break;
                    }
                }
                catch (Exception e) { HandleVoiceChatServiceDisabled(e, resetData: true); }
            }
        }

        public void RejectCall()
        {
            //We can reject a call only if we are receiving a call
            if (status.Value is not VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_REJECTING_CALL);

            RejectCallAsync(callId1.Value, cts.Token).Forget();
            return;

            async UniTaskVoid RejectCallAsync(string callId, CancellationToken ct)
            {
                try
                {
                    RejectPrivateVoiceChatResponse response = await voiceChatService.RejectPrivateVoiceChatAsync(callId, ct);

                    switch (response.ResponseCase)
                    {
                        //When the call has been ended
                        case RejectPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                            UpdateStatus(VoiceChatStatus.DISCONNECTED);
                            break;
                        default:
                            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                            break;
                    }
                }
                catch (Exception e) { HandleVoiceChatServiceDisabled(e, resetData: false); }
            }
        }

        private void ResetVoiceChatData()
        {
            SetCallId(string.Empty);
            connectionUrl = string.Empty;
            CurrentTargetWallet = string.Empty;
        }

        private void HandleVoiceChatServiceDisabled(Exception e, bool resetData = false)
        {
            ReportHub.LogWarning($"Voice chat service is disabled: {e.Message}", new ReportData(ReportCategory.VOICE_CHAT));

            if (resetData) { ResetVoiceChatData(); }

            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        public void HandleLivekitConnectionFailed()
        {
            if (status.Value is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }

        public void HandleLivekitConnectionEnded()
        {
            if (status.Value is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.DISCONNECTED);
            }
        }

        public void UpdateStatus(VoiceChatStatus newStatus)
        {
            UpdateStatusAsync().Forget();

            async UniTaskVoid UpdateStatusAsync()
            {
                await UniTask.SwitchToMainThread();
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} New status is {newStatus}");
                status.Value = newStatus;
            }
        }

        void IVoiceChatCallStatusServiceBase.ResetVoiceChatData()
        {
            ResetVoiceChatData();
        }
    }
}
