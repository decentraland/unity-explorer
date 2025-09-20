using Cysharp.Threading.Tasks;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using System;
using System.Threading;

namespace DCL.VoiceChat
{
    public class CommunityVoiceChatCallStatusServiceNull : ICommunityVoiceChatCallStatusService
    {
        public IReadonlyReactiveProperty<VoiceChatStatus> Status { get; }
        public IReadonlyReactiveProperty<string> CallId { get; }
        public string ConnectionUrl { get; }

        public void StartCall(string target) { }
        public void HangUp() { }
        public void HandleLivekitConnectionFailed() { }
        public void HandleLivekitConnectionEnded() { }
        public void UpdateStatus(VoiceChatStatus newStatus) { }
        public void ResetVoiceChatData() { }
        public void SetCallId(string newCallId) { }
        public void Dispose() { }

        public bool HasActiveVoiceChatCall(string communityId) =>
            false;

        public ReactiveProperty<bool>? SubscribeToCommunityUpdates(string communityId) =>
            null;

        public bool TryGetActiveCommunityVoiceChat(string communityId, out ActiveCommunityVoiceChat activeCommunityVoiceChat)
        {
            activeCommunityVoiceChat = default;
            return false;
        }

        public UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken cancellationToken = default) =>
            new ();

        public void RequestToSpeakInCurrentCall() { }

        public void PromoteToSpeakerInCurrentCall(string walletId) { }

        public void DemoteFromSpeakerInCurrentCall(string walletId) { }

        public void KickPlayerFromCurrentCall(string walletId) { }

        public void DenySpeakerInCurrentCall(string walletId) { }

        public void LowerHandInCurrentCall() { }

        public void EndStreamInCurrentCall() { }
    }
}
