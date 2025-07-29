using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public enum VoiceChatType
    {
        NONE,
        PRIVATE,
        COMMUNITY,
    }

    public enum VoiceChatPanelSize
    {
        DEFAULT,
        EXPANDED,
    }

    /// <summary>
    ///     Interface for systems that need to read or subscribe to voice chat state
    /// </summary>
    public interface IVoiceChatOrchestratorState
    {
        IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus { get; }
        IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize { get; }
    }

    /// <summary>
    ///     Interface for systems that need to perform voice chat actions
    /// </summary>
    public interface IVoiceChatOrchestratorActions
    {
        void StartCall(string callId, VoiceChatType callType);

        void AcceptPrivateCall();

        void HangUp();

        void RejectCall();

        void HandleConnectionError();
    }

    public interface IVoiceChatOrchestratorUIEvents
    {
        void ChangePanelSize(VoiceChatPanelSize panelSize);
    }

    /// <summary>
    ///     Interface for systems that need all interfaces (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, IVoiceChatOrchestratorUIEvents
    {
        string CurrentConnectionUrl { get; }
        string CurrentCallId { get; }
        IPrivateVoiceChatCallStatusService PrivateStatusService { get; }
        ICommunityVoiceChatCallStatusService CommunityStatusService { get; }
        VoiceChatParticipantsStateService ParticipantsStateService { get; }
    }

    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;
        private readonly IVoiceService rpcPrivateVoiceChatService;
        private readonly ICommunityVoiceService rpcCommunityVoiceChatService;

        private readonly IDisposable privateStatusSubscription;
        private readonly IDisposable communityStatusSubscription;

        private readonly ReactiveProperty<VoiceChatType> currentVoiceChatType = new (VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> currentCallStatus = new (VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize = new (VoiceChatPanelSize.DEFAULT);

        private VoiceChatCallStatusServiceBase activeCallStatusService;

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => currentVoiceChatType;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus => currentCallStatus;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => currentVoiceChatPanelSize;

        public string CurrentConnectionUrl => activeCallStatusService?.ConnectionUrl ?? string.Empty;

        /// <summary>
        ///     For Private Conversations, it is the Wallet Address of the other user, for Communities, it is the Community ID
        /// </summary>
        public string CurrentCallId => activeCallStatusService?.CallId ?? string.Empty;
        public IPrivateVoiceChatCallStatusService PrivateStatusService => privateVoiceChatCallStatusService;
        public ICommunityVoiceChatCallStatusService CommunityStatusService => communityVoiceChatCallStatusService;
        public VoiceChatParticipantsStateService ParticipantsStateService { get; }

        public VoiceChatOrchestrator(
            PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService,
            CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService,
            IVoiceService rpcPrivateVoiceChatService,
            ICommunityVoiceService rpcCommunityVoiceChatService,
            VoiceChatParticipantsStateService participantsStateService)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.communityVoiceChatCallStatusService = communityVoiceChatCallStatusService;
            this.rpcPrivateVoiceChatService = rpcPrivateVoiceChatService;
            this.rpcCommunityVoiceChatService = rpcCommunityVoiceChatService;
            this.ParticipantsStateService = participantsStateService;

            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;

            //I think we dont need this one for now, as these updates are only for communities data, doesnt affect the relation between both states.
            //rpcCommunityVoiceChatService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;
            privateStatusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
            communityStatusSubscription = communityVoiceChatCallStatusService.Status.Subscribe(OnCommunityVoiceChatStatusChanged);
        }

        public void Dispose()
        {
            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;

            //rpcCommunityVoiceChatService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
            privateStatusSubscription?.Dispose();
            communityStatusSubscription?.Dispose();

            currentVoiceChatType?.Dispose();
            currentCallStatus?.Dispose();
            currentVoiceChatPanelSize?.Dispose();
            ParticipantsStateService?.Dispose();
        }

        public void StartCall(string callId, VoiceChatType callType)
        {
            if (currentVoiceChatType.Value != VoiceChatType.NONE)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot start a call while in another call");
                return;
            }

            SetActiveCallService(callType);
            activeCallStatusService.StartCall(callId);
        }

        public void AcceptPrivateCall()
        {
            if (activeCallStatusService is PrivateVoiceChatCallStatusService privateService)
                privateService.AcceptCall();
            else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "AcceptCall not supported for current voice chat type");
        }

        public void KickPlayer(string communityId, string walletId)
        {
            if (activeCallStatusService is CommunityVoiceChatCallStatusService communityService)
                communityService.KickPlayer(communityId, walletId);
            else
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "KickPlayer is not supported for current voice chat type");
        }

        public void HangUp() =>
            activeCallStatusService?.HangUp();

        public void RejectCall()
        {
            if (activeCallStatusService is PrivateVoiceChatCallStatusService privateService)
                privateService.RejectCall();
            else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "RejectCall not supported for current voice chat type");
        }

        public void HandleConnectionError()
        {
            activeCallStatusService?.HandleLivekitConnectionFailed();
        }

        private void OnCommunityVoiceChatUpdateReceived(CommunityVoiceChatUpdate update) { }

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
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR || status== VoiceChatStatus.VOICE_CHAT_BUSY)
            {
                if (currentVoiceChatType.Value == VoiceChatType.PRIVATE) { SetActiveCallService(VoiceChatType.NONE); }
            }
            else if (status == VoiceChatStatus.VOICE_CHAT_STARTING_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_STARTED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.PRIVATE);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void OnCommunityVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a community call
            if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) { currentCallStatus.Value = status; }

            // Handle transitions to/from community call
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR || status== VoiceChatStatus.VOICE_CHAT_BUSY)
            {
                if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY) { SetActiveCallService(VoiceChatType.NONE); }
            }
            else if (status == VoiceChatStatus.VOICE_CHAT_STARTING_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_STARTED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetActiveCallService(VoiceChatType.COMMUNITY);
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatType.Value}");
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
    }
}
