using CommunicationData.URLHelpers;
using DCL.Profiles;
using DCL.Utilities;

namespace DCL.VoiceChat
{
    public class VoiceChatParticipantState
    {
        public Profile.CompactInfo Profile { get; internal set; }

        public string WalletId => Profile.UserId;
        public ReactiveProperty<bool> IsSpeaking { get; }
        public string Name => Profile.Name;
        public bool HasClaimedName => Profile.HasClaimedName;
        public URLAddress ProfilePictureUrl => Profile.FaceSnapshotUrl;
        public ReactiveProperty<bool> IsRequestingToSpeak { get; }
        public ReactiveProperty<bool> IsSpeaker { get; }
        public ReactiveProperty<bool> IsMuted { get; }
        public ReactiveProperty<VoiceChatParticipantCommunityRole> Role { get; }

        private VoiceChatParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<bool> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<VoiceChatParticipantCommunityRole> role,
            ReactiveProperty<bool> isMuted)
        {
            Profile = new Profile.CompactInfo { UserId = walletId };
            IsSpeaking = isSpeaking;
            IsRequestingToSpeak = isRequestingToSpeak;
            IsSpeaker = isSpeaker;
            Role = role;
            IsMuted = isMuted;
        }

        public static VoiceChatParticipantState CreateDefault(string walletId) =>
            new (
                walletId,
                new ReactiveProperty<bool>(false),
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
