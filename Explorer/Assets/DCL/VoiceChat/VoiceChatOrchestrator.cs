using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;
using System.Threading;

namespace DCL.VoiceChat
{
    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private const string TAG = nameof(VoiceChatOrchestrator);

        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;

        private readonly IDisposable privateStatusSubscription;
        private readonly IDisposable communityStatusSubscription;

        private readonly ReactiveProperty<VoiceChatType> currentVoiceChatType = new (VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> currentCallStatus = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize = new (VoiceChatPanelSize.DEFAULT);

        private VoiceChatCallStatusServiceBase activeCallStatusService;

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => currentVoiceChatType;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus => currentCallStatus;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => currentVoiceChatPanelSize;
        public IReadonlyReactiveProperty<string> CurrentCommunityId => communityVoiceChatCallStatusService.CallId;
        public string CurrentConnectionUrl => activeCallStatusService?.ConnectionUrl ?? string.Empty;
        public VoiceChatParticipantsStateService ParticipantsStateService { get; }

        public IReadonlyReactiveProperty<VoiceChatStatus> CommunityCallStatus => communityVoiceChatCallStatusService.Status;

        public IReadonlyReactiveProperty<VoiceChatStatus> PrivateCallStatus => privateVoiceChatCallStatusService.Status;

        public string CurrentTargetWallet => privateVoiceChatCallStatusService.CurrentTargetWallet;

        public VoiceChatOrchestrator(
            PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService,
            CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService,
            VoiceChatParticipantsStateService participantsStateService)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.communityVoiceChatCallStatusService = communityVoiceChatCallStatusService;
            ParticipantsStateService = participantsStateService;

            privateVoiceChatCallStatusService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;

            privateStatusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
            communityStatusSubscription = communityVoiceChatCallStatusService.Status.Subscribe(OnCommunityVoiceChatStatusChanged);
        }

        public void Dispose()
        {
            privateVoiceChatCallStatusService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;

            privateStatusSubscription?.Dispose();
            communityStatusSubscription?.Dispose();

            currentVoiceChatType?.Dispose();
            currentCallStatus?.Dispose();
            currentVoiceChatPanelSize?.Dispose();
            ParticipantsStateService?.Dispose();
        }

        public void StartCall(string callId, VoiceChatType callType)
        {
            if (VoiceChatCallTypeValidator.IsNoActiveCall(currentVoiceChatType.Value))
            {
                SetActiveCallService(callType);
                activeCallStatusService.StartCall(callId);
            }
        }

        public void AcceptPrivateCall()
        {
            if (VoiceChatCallTypeValidator.IsPrivateCall(currentVoiceChatType.Value))
                privateVoiceChatCallStatusService.AcceptCall();
        }

        public void HangUp() =>
            activeCallStatusService?.HangUp();

        public void RejectCall()
        {
            if (VoiceChatCallTypeValidator.IsPrivateCall(currentVoiceChatType.Value))
                privateVoiceChatCallStatusService.RejectCall();
        }

        public void HandleConnectionEnded()
        {
            communityVoiceChatCallStatusService.HandleLivekitConnectionEnded();
            privateVoiceChatCallStatusService.HandleLivekitConnectionEnded();
        }

        public void HandleConnectionError()
        {
            activeCallStatusService?.HandleLivekitConnectionFailed();
        }

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (currentVoiceChatType.Value != VoiceChatType.COMMUNITY)
            {
                SetActiveCallService(VoiceChatType.PRIVATE);
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a private call
            if (currentVoiceChatType.Value == VoiceChatType.PRIVATE) { currentCallStatus.Value = status; }

            // Handle transitions to/from private call
            if (status.IsNotConnected())
            {
                if (currentVoiceChatType.Value == VoiceChatType.PRIVATE) SetActiveCallService(VoiceChatType.NONE);
            }
            else if (status is VoiceChatStatus.VOICE_CHAT_STARTING_CALL
                     or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL
                     or VoiceChatStatus.VOICE_CHAT_STARTED_CALL
                     or VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.PRIVATE);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void OnCommunityVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a community call
            if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) { currentCallStatus.Value = status; }

            // Handle transitions to/from community call
            if (status.IsNotConnected())
            {
                if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) SetActiveCallService(VoiceChatType.NONE);
            }
            else if (status is VoiceChatStatus.VOICE_CHAT_STARTING_CALL
                     or VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL
                     or VoiceChatStatus.VOICE_CHAT_STARTED_CALL
                     or VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.COMMUNITY);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void SetActiveCallService(VoiceChatType newType)
        {
            currentVoiceChatType.UpdateValue(newType);

            switch (newType)
            {
                case VoiceChatType.NONE:
                    activeCallStatusService = null;
                    break;
                case VoiceChatType.PRIVATE:
                    activeCallStatusService = privateVoiceChatCallStatusService;
                    break;
                case VoiceChatType.COMMUNITY:
                    activeCallStatusService = communityVoiceChatCallStatusService;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(newType), newType, null);
            }
        }

        public void ChangePanelSize(VoiceChatPanelSize panelSize)
        {
            currentVoiceChatPanelSize.Value = panelSize;
        }

        public void JoinCommunityVoiceChat(string communityId, CancellationToken ct)
        {
            if (VoiceChatCallTypeValidator.IsNoActiveCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.JoinCommunityVoiceChatAsync(communityId, ct).Forget();
        }

        public void RequestToSpeakInCurrentCall()
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.RequestToSpeakInCurrentCall();
        }

        public void PromoteToSpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.PromoteToSpeakerInCurrentCall(walletId);
        }

        public void DenySpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.DenySpeakerInCurrentCall(walletId);
        }

        public void DemoteFromSpeakerInCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.DemoteFromSpeakerInCurrentCall(walletId);
        }

        public void KickPlayerFromCurrentCall(string walletId)
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.KickPlayerFromCurrentCall(walletId);
        }

        public void EndStreamInCurrentCall()
        {
            if (VoiceChatCallTypeValidator.IsCommunityCall(currentVoiceChatType.Value))
                communityVoiceChatCallStatusService.EndStreamInCurrentCall();
        }

        public bool HasActiveVoiceChatCall(string communityId) =>
            communityVoiceChatCallStatusService.HasActiveVoiceChatCall(communityId);

        public bool TryGetActiveCommunityData(string communityId, out ActiveCommunityVoiceChat activeCommunityData) =>
            communityVoiceChatCallStatusService.TryGetActiveCommunityVoiceChat(communityId, out activeCommunityData);

        public ReactiveProperty<bool> SubscribeToCommunityUpdates(string communityId) =>
            communityVoiceChatCallStatusService.SubscribeToCommunityUpdates(communityId);
    }
}
