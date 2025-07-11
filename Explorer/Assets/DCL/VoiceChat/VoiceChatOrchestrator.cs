using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using DCL.Web3;
using Decentraland.SocialService.V2;
using System;

namespace DCL.VoiceChat
{
    public enum CurrentVoiceChatType
    {
        NONE,
        PRIVATE,
        COMMUNITY,
    }

    /// <summary>
    /// Interface for systems that need to read or subscribe to voice chat state
    /// </summary>
    public interface IVoiceChatState
    {
        CurrentVoiceChatType GetCurrentVoiceChatType();
        VoiceChatStatus GetCurrentPrivateVoiceChatStatus();
        VoiceChatStatus GetCurrentCommunityVoiceChatStatus();

        IReadonlyReactiveProperty<CurrentVoiceChatType> CurrentVoiceChat { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> PrivateVoiceChatStatus { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CommunityVoiceChatStatus { get; }
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

    /// <summary>
    /// Interface for systems that need both state and actions (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : IVoiceChatState, IVoiceChatActions
    {
    }


    public class VoiceChatOrchestrator : IDisposable, IVoiceChatOrchestrator
    {
        private readonly VoiceChatEventBus voiceChatEventBus;
        private readonly IVoiceChatCallStatusService privateVoiceChatCallStatusService;
        private readonly IVoiceService rpcPrivateVoiceChatService;

        private readonly IDisposable statusSubscription;

        private readonly ReactiveProperty<CurrentVoiceChatType> currentVoiceChatProperty = new(CurrentVoiceChatType.NONE);
        private readonly ReactiveProperty<VoiceChatStatus> privateVoiceChatStatusProperty = new(VoiceChatStatus.DISCONNECTED);
        private readonly ReactiveProperty<VoiceChatStatus> communityVoiceChatStatusProperty = new(VoiceChatStatus.DISCONNECTED);

        public IReadonlyReactiveProperty<CurrentVoiceChatType> CurrentVoiceChat => currentVoiceChatProperty;
        public IReadonlyReactiveProperty<VoiceChatStatus> PrivateVoiceChatStatus => privateVoiceChatStatusProperty;
        public IReadonlyReactiveProperty<VoiceChatStatus> CommunityVoiceChatStatus => communityVoiceChatStatusProperty;

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
            voiceChatEventBus.StartPrivateVoiceChatRequested -= OnStartVoiceChatRequested;
            rpcPrivateVoiceChatService.PrivateVoiceChatUpdateReceived -= OnPrivateVoiceChatUpdateReceived;
            statusSubscription?.Dispose();

            currentVoiceChatProperty?.Dispose();
            privateVoiceChatStatusProperty?.Dispose();
            communityVoiceChatStatusProperty?.Dispose();
        }

        // IVoiceChatState implementation
        public CurrentVoiceChatType GetCurrentVoiceChatType() => currentVoiceChatProperty.Value;
        public VoiceChatStatus GetCurrentPrivateVoiceChatStatus() => privateVoiceChatStatusProperty.Value;
        public VoiceChatStatus GetCurrentCommunityVoiceChatStatus() => communityVoiceChatStatusProperty.Value;

        // IVoiceChatActions implementation
        public void StartPrivateCall(Web3Address walletId)
        {
            if (currentVoiceChatProperty.Value != CurrentVoiceChatType.COMMUNITY)
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
            if (currentVoiceChatProperty.Value != CurrentVoiceChatType.COMMUNITY)
            {
                privateVoiceChatCallStatusService.OnPrivateVoiceChatUpdateReceived(update);
            }
        }

        private void OnPrivateVoiceChatStatusChanged(VoiceChatStatus status)
        {
            privateVoiceChatStatusProperty.Value = status;

            if (currentVoiceChatProperty.Value != CurrentVoiceChatType.PRIVATE) return;

            if (status == VoiceChatStatus.DISCONNECTED || status == VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
            {
                SetVoiceChatType(CurrentVoiceChatType.NONE);
            }
            else
            {
                SetVoiceChatType(CurrentVoiceChatType.PRIVATE);
            }

            ReportHub.Log(ReportCategory.VOICE_CHAT, $"Switched Orchestrator state to {currentVoiceChatProperty.Value}");
        }

        private void OnStartVoiceChatRequested(Web3Address walletId)
        {
            StartPrivateCall(walletId);
        }

        private void SetVoiceChatType(CurrentVoiceChatType newType)
        {
            if (currentVoiceChatProperty.Value != newType)
            {
                currentVoiceChatProperty.Value = newType;
            }
        }
    }
}
