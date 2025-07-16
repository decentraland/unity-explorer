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

        private readonly IDisposable privateStatusSubscription;
        private readonly IDisposable communityStatusSubscription;

        private readonly ReactiveProperty<VoiceChatType> voiceChatTypeProperty = new(VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> currentCallStatusProperty = new(VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> voiceChatPanelSizeProperty = new(VoiceChatPanelSize.DEFAULT);

        private VoiceChatCallStatusServiceBase activeCallStatusService;

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => voiceChatTypeProperty;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus => currentCallStatusProperty;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => voiceChatPanelSizeProperty;

        public string CurrentRoomUrl => activeCallStatusService?.RoomUrl ?? string.Empty;
        public IPrivateVoiceChatCallStatusService PrivateStatusService => privateVoiceChatCallStatusService;
        public ICommunityVoiceChatCallStatusService CommunityStatusService => communityVoiceChatCallStatusService;

        public VoiceChatOrchestrator(
            PrivateVoiceChatCallStatusService privateVoiceChatCallStatusService,
            CommunityVoiceChatCallStatusService communityVoiceChatCallStatusService,
            IVoiceService rpcPrivateVoiceChatService)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.communityVoiceChatCallStatusService = communityVoiceChatCallStatusService;
            this.rpcPrivateVoiceChatService = rpcPrivateVoiceChatService;

            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            privateStatusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
            communityStatusSubscription = communityVoiceChatCallStatusService.Status.Subscribe(OnCommunityVoiceChatStatusChanged);
        }

        public void Dispose()
        {
            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
            privateStatusSubscription?.Dispose();
            communityStatusSubscription?.Dispose();

            voiceChatTypeProperty?.Dispose();
            currentCallStatusProperty?.Dispose();
            voiceChatPanelSizeProperty?.Dispose();
        }

        // IVoiceChatActions implementation
        public void StartPrivateCall(Web3Address walletId)
        {
            if (voiceChatTypeProperty.Value == VoiceChatType.COMMUNITY)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "Cannot start private call while in community call");
                return;
            }

            SetActiveCallService(privateVoiceChatCallStatusService);
            privateVoiceChatCallStatusService.StartCall(walletId);
        }

        public void AcceptCall()
        {
            if (activeCallStatusService is PrivateVoiceChatCallStatusService  privateService)
                privateService.AcceptCall();
            else
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "AcceptCall not supported for current voice chat type");
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

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (voiceChatTypeProperty.Value != VoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            if (voiceChatTypeProperty.Value == VoiceChatType.PRIVATE)
            {
                currentCallStatusProperty.Value = status;
            }

            if (voiceChatTypeProperty.Value != VoiceChatType.PRIVATE) return;

            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                SetVoiceChatType(VoiceChatType.NONE);
                activeCallStatusService = null;
            }
            else
            {
                SetVoiceChatType(VoiceChatType.PRIVATE);
                activeCallStatusService = privateVoiceChatCallStatusService;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {voiceChatTypeProperty.Value}");
        }

        private void OnCommunityVoiceChatStatusChanged(VoiceChatStatus status)
        {
            if (voiceChatTypeProperty.Value == VoiceChatType.COMMUNITY)
            {
                currentCallStatusProperty.Value = status;
            }

            if (voiceChatTypeProperty.Value != VoiceChatType.COMMUNITY) return;

            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                SetVoiceChatType(VoiceChatType.NONE);
                activeCallStatusService = null;
            }
            else
            {
                SetVoiceChatType(VoiceChatType.COMMUNITY);
                activeCallStatusService = communityVoiceChatCallStatusService;
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {voiceChatTypeProperty.Value}");
        }

        private void SetVoiceChatType(VoiceChatType newType)
        {
            if (voiceChatTypeProperty.Value != newType)
            {
                voiceChatTypeProperty.Value = newType;
            }
        }

        private void SetActiveCallService(VoiceChatCallStatusServiceBase service)
        {
            activeCallStatusService = service;
        }

        public void ChangePanelSize(VoiceChatPanelSize panelSize)
        {
            voiceChatPanelSizeProperty.Value = panelSize;
        }
    }
}
