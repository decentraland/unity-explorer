using DCL.Utilities;

namespace DCL.VoiceChat
{
    public class VoiceChatParticipantState
    {
        public string WalletId { get; set; }
        public ReactiveProperty<bool> IsSpeaking { get; set; }
        public ReactiveProperty<string?> Name { get; set; }
        public ReactiveProperty<bool?> HasClaimedName { get; set; }
        public ReactiveProperty<string?> ProfilePictureUrl { get; set; }
        public ReactiveProperty<bool> IsRequestingToSpeak { get; set; }
        public ReactiveProperty<bool> IsSpeaker { get; set; }
        public ReactiveProperty<bool> IsMuted { get; set; }
        public ReactiveProperty<VoiceChatParticipantsStateService.UserCommunityRoleMetadata> Role { get; set; }

        public VoiceChatParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<string?> name, ReactiveProperty<bool?> hasClaimedName, ReactiveProperty<string?> profilePictureUrl,
            ReactiveProperty<bool> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<VoiceChatParticipantsStateService.UserCommunityRoleMetadata> role, ReactiveProperty<bool> isMuted)
        {
            WalletId = walletId;
            IsSpeaking = isSpeaking;
            Name = name;
            HasClaimedName = hasClaimedName;
            ProfilePictureUrl = profilePictureUrl;
            IsRequestingToSpeak = isRequestingToSpeak;
            IsSpeaker = isSpeaker;
            Role = role;
            IsMuted = isMuted;
        }
    }
}
