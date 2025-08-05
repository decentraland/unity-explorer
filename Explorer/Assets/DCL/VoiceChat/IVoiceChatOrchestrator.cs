using DCL.Utilities;
using DCL.VoiceChat.Services;
using System.Threading;

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
    ///     Interface for systems that need all interfaces (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : ICommunityCallOrchestrator, IPrivateCallOrchestrator
    {
        string CurrentConnectionUrl { get; }
        string CurrentCallId { get; }
    }

    public interface IVoiceChatOrchestratorState
    {
        IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType { get; }
        IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus { get; }
        VoiceChatParticipantsStateService ParticipantsStateService { get; }
    }

    public interface IPrivateCallState
    {
        IReadonlyReactiveProperty<VoiceChatStatus> PrivateCallStatus { get; }
        string CurrentTargetWallet { get; }
    }

    public interface ICommunityCallState
    {
        IReadonlyReactiveProperty<VoiceChatStatus> CommunityCallStatus { get; }
        string CurrentCommunityId { get; }
        bool HasActiveVoiceChatCall(string communityId);
        ReactiveProperty<bool> SubscribeToCommunityUpdates(string communityId);
        bool TryGetActiveCommunityData(string communityId, out ActiveCommunityVoiceChat activeCommunityData);
    }

    public interface IVoiceChatOrchestratorActions
    {
        void StartCall(string callId, VoiceChatType callType);
        void HangUp();
        void HandleConnectionError();
        void ChangePanelSize(VoiceChatPanelSize panelSize);
    }

    public interface IPrivateCallActions
    {
        void AcceptPrivateCall();
        void RejectCall();
    }

    public interface ICommunityCallActions
    {
        void JoinCommunityVoiceChat(string communityId, CancellationToken ct);
        void RequestToSpeakInCurrentCall();
        void PromoteToSpeakerInCurrentCall(string walletId);
        void DenySpeakerInCurrentCall(string walletId);
        void DemoteFromSpeakerInCurrentCall(string walletId);
        void KickPlayerFromCurrentCall(string walletId);
    }

    // Combined interfaces for convenience
    public interface IPrivateCallOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, IPrivateCallActions, IPrivateCallState
    { }

    public interface ICommunityCallOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, ICommunityCallActions, ICommunityCallState
    { }
}
