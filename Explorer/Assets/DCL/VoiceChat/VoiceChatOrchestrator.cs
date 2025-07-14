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
    public interface IVoiceChatState
    {
        IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentPrivateVoiceChatStatus { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentCommunityVoiceChatStatus { get; }
        IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize { get; }
    }

    /// <summary>
    /// Interface for systems that need to perform voice chat actions
    /// </summary>
    public interface IVoiceChatActions
    {
        void StartPrivateCall(Web3Address walletId);
        void AcceptCall();
        void HangUp();
        void RejectCall();
    }

    public interface IVoiceChatUIEvents
    {
        void ChangePanelSize(VoiceChatPanelSize panelSize);

    }

    /// <summary>
    /// Interface for systems that need both state and actions (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : IVoiceChatState, IVoiceChatActions, IVoiceChatUIEvents
    {
    }


    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private readonly IVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly IVoiceService rpcPrivateVoiceChatService;

        private readonly IDisposable statusSubscription;

        private readonly ReactiveProperty<VoiceChatType> voiceChatTypeProperty = new(VoiceChat.VoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> privateVoiceChatStatusProperty = new(VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatStatus> communityVoiceChatStatusProperty = new(VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatPanelSize> voiceChatPanelSizeProperty = new(VoiceChatPanelSize.DEFAULT);

        public IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType => voiceChatTypeProperty;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentPrivateVoiceChatStatus => privateVoiceChatStatusProperty;
        public IReadonlyReactiveProperty<VoiceChatStatus> CurrentCommunityVoiceChatStatus => communityVoiceChatStatusProperty;
        public IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize => voiceChatPanelSizeProperty;

        public VoiceChatOrchestrator(
            IVoiceChatCallStatusService privateVoiceChatCallStatusService,
            IVoiceService rpcPrivateVoiceChatService)
        {
            this.privateVoiceChatCallStatusService = privateVoiceChatCallStatusService;
            this.rpcPrivateVoiceChatService = rpcPrivateVoiceChatService;

            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived += OnPrivateVoiceChatUpdateReceived;
            statusSubscription = privateVoiceChatCallStatusService.Status.Subscribe(OnPrivateVoiceChatStatusChanged);
        }

        public void Dispose()
        {
            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
            statusSubscription?.Dispose();

            voiceChatTypeProperty?.Dispose();
            privateVoiceChatStatusProperty?.Dispose();
            communityVoiceChatStatusProperty?.Dispose();
        }

        // IVoiceChatActions implementation
        public void StartPrivateCall(Web3Address walletId)
        {
            if (voiceChatTypeProperty.Value != VoiceChat.VoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.StartCall(walletId);
            }
            // TODO: Handle community call state - show proper message
        }

        public void AcceptCall() => privateVoiceChatCallStatusService.AcceptCall();
        public void HangUp() => privateVoiceChatCallStatusService.HangUp();
        public void RejectCall() => privateVoiceChatCallStatusService.RejectCall();

        private void OnPrivateVoiceChatUpdateReceived(PrivateVoiceChatUpdate update)
        {
            if (voiceChatTypeProperty.Value != VoiceChat.VoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            privateVoiceChatStatusProperty.Value = status;

            if (voiceChatTypeProperty.Value != VoiceChat.VoiceChatType.PRIVATE) return;

            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                SetVoiceChatType(VoiceChat.VoiceChatType.NONE);
            }
            else
            {
                SetVoiceChatType(VoiceChat.VoiceChatType.PRIVATE);
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

        public void ChangePanelSize(VoiceChatPanelSize panelSize)
        {
            voiceChatPanelSizeProperty.Value = panelSize;
        }
    }
}
