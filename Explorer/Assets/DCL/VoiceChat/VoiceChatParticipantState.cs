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
        public ReactiveProperty<VoiceChatParticipantCommunityRole> Role { get; set; }

        private VoiceChatParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<string?> name, ReactiveProperty<bool?> hasClaimedName, ReactiveProperty<string?> profilePictureUrl,
            ReactiveProperty<bool> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<VoiceChatParticipantCommunityRole> role, ReactiveProperty<bool> isMuted)
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

        public static VoiceChatParticipantState CreateDefault(string walletId) =>
            new (
                walletId,
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool?>(false),
                new ReactiveProperty<string?>(null),
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<bool>(false),
                new ReactiveProperty<VoiceChatParticipantCommunityRole>(VoiceChatParticipantCommunityRole.NONE),
                new ReactiveProperty<bool>(false)
            );
    }

    public enum VoiceChatParticipantCommunityRole
    {
        NONE,
        USER,
        MODERATOR,
        OWNER,
    }

}
