using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3;
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
        EXPANDED
    }

    /// <summary>
    /// Interface for systems that need to read or subscribe to voice chat state
    /// </summary>
    public interface IVoiceChatOrchestratorState
    {
        IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus { get; }
        IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize { get; }
    }

    /// <summary>
    /// Interface for systems that need to perform voice chat actions
    /// </summary>
    public interface IVoiceChatOrchestratorActions
    {
        void StartPrivateCall(Web3Address walletId);
        void AcceptCall();
        void HangUp();
        void RejectCall();
        void HandleConnectionError();
    }

    public interface IVoiceChatOrchestratorUIEvents
    {
        void ChangePanelSize(VoiceChatPanelSize panelSize);
    }

    /// <summary>
    /// Interface for systems that need all interfaces (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, IVoiceChatOrchestratorUIEvents
    {
        string CurrentRoomUrl { get; }
        IPrivateVoiceChatCallStatusService PrivateStatusService { get; }
        ICommunityVoiceChatCallStatusService CommunityStatusService { get; }
     }

    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private readonly PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService;
        private readonly IVoiceService rpcPrivateVoiceChatService;
        private readonly ICommunityVoiceService rpcCommunityVoiceChatService;

        private readonly IDisposable privateStatusSubscription;
        private readonly IDisposable communityStatusSubscription;

        private readonly ReactiveProperty<VoiceChatType> currentVoiceChatType = new(VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> currentCallStatus = new(VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize = new(VoiceChatPanelSize.DEFAULT);

        private VoiceChatCallStatusServiceBase activeCallStatusService;

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => currentVoiceChatType;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus => currentCallStatus;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => currentVoiceChatPanelSize;

        public string CurrentRoomUrl => activeCallStatusService?.RoomUrl ?? string.Empty;
        public IPrivateVoiceChatCallStatusService PrivateStatusService => privateVoiceChatCallStatusService;
        public ICommunityVoiceChatCallStatusService CommunityStatusService => communityVoiceChatCallStatusService;

        public VoiceChatOrchestrator(
            PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService,
            CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService,
            IVoiceService rpcPrivateVoiceChatService,
            ICommunityVoiceService rpcCommunityVoiceChatService)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.communityVoiceChatCallStatusService = communityVoiceChatCallStatusService;
            this.rpcPrivateVoiceChatService = rpcPrivateVoiceChatService;
            this.rpcCommunityVoiceChatService = rpcCommunityVoiceChatService;

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
        }

        public void StartPrivateCall(Web3Address walletId)
        {
            if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot start private call while in community call");
                return;
            }

            SetActiveCallService(privateVoiceChatCallStatusService);
            privateVoiceChatCallStatusService.StartCall(walletId);
        }

        public void StartCommunityCall(string communityId)
        {
            if (currentVoiceChatType.Value == VoiceChatType.PRIVATE)
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Cannot start community call when in a private call");
                return;
            }

            SetActiveCallService(communityVoiceChatCallStatusService);
            communityVoiceChatCallStatusService.StartCall(communityId);
        }

        public void AcceptCall()
        {
            if (activeCallStatusService is PrivateVoiceChatCallStatusService  privateService)
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

        public void HangUp() => activeCallStatusService?.HangUp();

        public void RejectCall()
        {
            if (activeCallStatusService is PrivateVoiceChatCallStatusService  privateService)
                privateService.RejectCall();
            else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "RejectCall not supported for current voice chat type");
        }

        public void HandleConnectionError()
        {
            activeCallStatusService?.HandleLivekitConnectionFailed();
        }

        private void OnCommunityVoiceChatUpdateReceived(CommunityVoiceChatUpdate update)
        {
        }

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (currentVoiceChatType.Value != VoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a private call
            if (currentVoiceChatType.Value == VoiceChatType.PRIVATE)
            {
                currentCallStatus.Value = status;
            }

            // Handle transitions to/from private call
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                if (currentVoiceChatType.Value == VoiceChatType.PRIVATE)
                {
                    SetVoiceChatType(VoiceChatType.NONE);
                    activeCallStatusService = null;
                }
            }
            else if (status == VoiceChatStatus.VOICE_CHAT_STARTING_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_STARTED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetVoiceChatType(VoiceChatType.PRIVATE);
                activeCallStatusService = privateVoiceChatCallStatusService;
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void OnCommunityVoiceChatStatusChanged(VoiceChatStatus status)
        {
            // Update call status if we're already in a community call
            if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY)
            {
                currentCallStatus.Value = status;
            }

            // Handle transitions to/from community call
            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                if (currentVoiceChatType.Value == VoiceChatType.COMMUNITY)
                {
                    SetVoiceChatType(VoiceChatType.NONE);
                    activeCallStatusService = null;
                }
            }
            else if (status == VoiceChatStatus.VOICE_CHAT_STARTING_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_RECEIVED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_STARTED_CALL ||
                     status == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                SetVoiceChatType(VoiceChatType.COMMUNITY);
                activeCallStatusService = communityVoiceChatCallStatusService;
                currentCallStatus.Value = status;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatType.Value}");
        }

        private void SetVoiceChatType(VoiceChatType newType)
        {
            if (currentVoiceChatType.Value != newType)
            {
                currentVoiceChatType.Value = newType;
            }
        }

        private void SetActiveCallService(VoiceChatCallStatusServiceBase service)
        {
            activeCallStatusService = service;
        }

        public void ChangePanelSize(VoiceChatPanelSize panelSize)
        {
            currentVoiceChatPanelSize.Value = panelSize;
        }
    }
}
