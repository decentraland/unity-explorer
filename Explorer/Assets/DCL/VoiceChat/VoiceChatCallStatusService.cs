using Cysharp.Threading.Tasks;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat
{
    public class VoiceChatCallStatusService : IVoiceChatCallStatusService
    {
        private readonly IVoiceService voiceChatService;

        private CancellationTokenSource cts;
        public VoiceChatStatus Status { get; private set; }
        public Web3Address CurrentTargetWallet { get; private set; }


        /// <summary>
        /// CallId is set when starting a call and when receiving a call
        /// </summary>
        public string CallId { get; private set; }

        /// <summary>
        /// Room url and Token are retrieved when accepting a call
        /// </summary>
        public string RoomUrl { get; private set; }

        public event IVoiceChatCallStatusService.VoiceChatStatusChangeDelegate StatusChanged;

        public VoiceChatCallStatusService(IVoiceService voiceChatService)
        {
            this.voiceChatService = voiceChatService;

            this.voiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            this.voiceChatService.Connected += OnConnected;
            this.voiceChatService.Disconnected += OnDisconnected;
            cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            voiceChatService?.Dispose();
            voiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
            voiceChatService.Connected -= OnConnected;
            voiceChatService.Disconnected -= OnDisconnected;
            cts?.Dispose();
        }

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            switch (update.Status)
            {
                case PrivateVoiceChatStatus.VoiceChatAccepted:
                    RoomUrl = update.Credentials.Url;
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                    break;
                case PrivateVoiceChatStatus.VoiceChatEnded:
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_ENDING_CALL);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRejected:
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.DISCONNECTED);
                    break;
                case PrivateVoiceChatStatus.VoiceChatRequested:
                    CallId = update.CallId;
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
                    break;
            }
        }

        private void OnConnected()
        {
            CheckIncomingCallAsync(cts.Token).Forget();
        }

        private async UniTaskVoid CheckIncomingCallAsync(CancellationToken ct)
        {
            var response = await voiceChatService.GetIncomingPrivateVoiceChatRequestAsync(ct);
            if (response.ResponseCase == GetIncomingPrivateVoiceChatRequestResponse.ResponseOneofCase.Ok)
            {
                CallId = response.Ok.CallId;
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL);
            }
        }

        private void OnDisconnected()
        {
            if (Status is not VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }

        public void StartCall(Web3Address walletId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status is not VoiceChatStatus.DISCONNECTED) return;

            CurrentTargetWallet = walletId;

            cts = cts.SafeRestart();

            //Setting starting call status to instantly disable call button
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            StartCallAsync(walletId, cts.Token).Forget();
        }

        private async UniTaskVoid StartCallAsync(Web3Address walletId, CancellationToken ct)
        {
            StartPrivateVoiceChatResponse response = await voiceChatService.StartPrivateVoiceChatAsync(walletId.ToString(), ct);

            switch (response.ResponseCase)
            {
                //When the call can be started
                case StartPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                    CallId = response.Ok.CallId;
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
                    break;

                //When the other user is already in a call or is already being called
                case StartPrivateVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                case StartPrivateVoiceChatResponse.ResponseOneofCase.ConflictingError:
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_USER_BUSY);
                    break;
                default:
                    ResetVoiceChatData();
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                    break;
            }
        }

        public void AcceptCall()
        {
            //We can accept a call only if we are receiving a call
            if (Status is not VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);

            AcceptCallAsync(CallId, cts.Token).Forget();
        }

        private async UniTaskVoid AcceptCallAsync(string callId, CancellationToken ct)
        {
            AcceptPrivateVoiceChatResponse response = await voiceChatService.AcceptPrivateVoiceChatAsync(callId, ct);

            switch (response.ResponseCase)
            {
                //When the call has been ended
                case AcceptPrivateVoiceChatResponse.ResponseOneofCase.Ok:
                    RoomUrl = response.Ok.Credentials.Url;
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                    break;
                default:
                    UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                    break;
            }
        }

        public void HangUp()
        {
            //We can stop a call only if we are starting a call or inside a call
            if (Status is not (VoiceChatStatus.VOICE_CHAT_STARTED_CALL or VoiceChatStatus.VOICE_CHAT_STARTING_CALL or VoiceChatStatus.VOICE_CHAT_IN_CALL)) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_ENDING_CALL);
            HangUpAsync(CallId, cts.Token).Forget();
        }

        private async UniTaskVoid HangUpAsync(string callId, CancellationToken ct)
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

        public void RejectCall()
        {
            //We can reject a call only if we are receiving a call
            if (Status is not VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_REJECTING_CALL);

            RejectCallAsync(CallId, cts.Token).Forget();
        }

        private async UniTaskVoid RejectCallAsync(string callId, CancellationToken ct)
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

        private void UpdateStatus(VoiceChatStatus newStatus)
        {
            Debug.Log($"New status is {newStatus}");
            Status = newStatus;
            StatusChanged?.Invoke(Status);
        }

        private void ResetVoiceChatData()
        {
            CallId = string.Empty;
            RoomUrl = string.Empty;
        }

        public void HandleConnectionFailed()
        {
            if (Status is VoiceChatStatus.VOICE_CHAT_IN_CALL or VoiceChatStatus.VOICE_CHAT_STARTED_CALL)
            {
                ResetVoiceChatData();
                UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
            }
        }
    }
}
