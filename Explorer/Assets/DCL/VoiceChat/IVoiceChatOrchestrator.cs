using DCL.Utilities;
using DCL.VoiceChat.Services;

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
        COLLAPSED,
        EXPANDED,
    }

    public enum VoiceChatPanelState
    {
        SELECTED, //When the user clicked on the panel
        FOCUSED, //When the user hovers over the panel/chat or clicks on the chat
        UNFOCUSED, //When the user deselects the panel AND unhovers from the panel/chat
        HIDDEN,
        NONE,
    }

    /// <summary>
    ///     Interface for systems that need all interfaces (like voice chat UI)
    /// </summary>
    public interface IVoiceChatOrchestrator : ICommunityCallOrchestrator, IPrivateCallOrchestrator
    {
        string CurrentConnectionUrl { get; }
    }

    public interface IVoiceChatOrchestratorState
    {
        IReadonlyReactiveProperty<VoiceChatType> CurrentVoiceChatType { get; }
        IReadonlyReactiveProperty<VoiceChatPanelSize> CurrentVoiceChatPanelSize { get; }
        IReadonlyReactiveProperty<VoiceChatStatus> CurrentCallStatus { get; }
        IReadonlyReactiveProperty<VoiceChatPanelState> CurrentVoiceChatPanelState { get; }
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
        IReadonlyReactiveProperty<string> CurrentCommunityId { get; }
        IReadonlyReactiveProperty<ActiveCommunityVoiceChat?> CurrentSceneActiveCommunityVoiceChatData { get; }
        bool HasActiveVoiceChatCall(string communityId);
        IReadonlyReactiveProperty<bool> CommunityConnectionUpdates(string communityId);
        bool TryGetActiveCommunityData(string communityId, out ActiveCommunityVoiceChat activeCommunityData);
        bool IsEqualToCurrentStreamingCommunity(string communityId);
    }

    public interface IVoiceChatOrchestratorActions
    {
        void StartCall(string callId, VoiceChatType callType);
        void HangUp();
        void HandleConnectionError();
        void HandleConnectionEnded();
        void ChangePanelSize(VoiceChatPanelSize panelSize);
        void ChangePanelState(VoiceChatPanelState panelState, bool force = false);
    }

    public interface IPrivateCallActions
    {
        void AcceptPrivateCall();
        void RejectCall();
    }

    public interface ICommunityCallActions
    {
        void JoinCommunityVoiceChat(string communityId, bool force = false);
        void RequestToSpeakInCurrentCall();
        void LowerHandInCurrentCall();
        void PromoteToSpeakerInCurrentCall(string walletId);
        void DenySpeakerInCurrentCall(string walletId);
        void DemoteFromSpeakerInCurrentCall(string walletId);
        void KickPlayerFromCurrentCall(string walletId);
        void NotifyMuteSpeakerInCurrentCall(string walletId, bool muted);
        void EndStreamInCurrentCall();
    }

    // Combined interfaces for convenience
    public interface IPrivateCallOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, IPrivateCallActions, IPrivateCallState
    { }

    public interface ICommunityCallOrchestrator : IVoiceChatOrchestratorState, IVoiceChatOrchestratorActions, ICommunityCallActions, ICommunityCallState
    { }
}
