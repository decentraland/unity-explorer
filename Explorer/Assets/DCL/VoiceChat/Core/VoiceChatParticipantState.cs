using CommunicationData.URLHelpers;
using DCL.Profiles;
using DCL.Utilities;
using DCL.Utility.Types;

namespace DCL.VoiceChat
{
    public class VoiceChatParticipantState
    {
        /// <summary>
        ///     None until the participant's identity is known — the local participant has no profile
        ///     before login and after the identity is cleared
        /// </summary>
        public Option<Profile.CompactInfo> Profile { get; internal set; }

        public Option<string> WalletId => Profile.Has ? Option<string>.Some(Profile.Value.UserId.Value) : Option<string>.None;
        public ReactiveProperty<bool> IsSpeaking { get; }
        public Option<string> Name => Profile.Has ? Option<string>.Some(Profile.Value.Name) : Option<string>.None;
        public bool HasClaimedName => Profile.Has && Profile.Value.HasClaimedName;
        public Option<URLAddress> ProfilePictureUrl => Profile.Has ? Option<URLAddress>.Some(Profile.Value.FaceSnapshotUrl) : Option<URLAddress>.None;
        public ReactiveProperty<bool> IsRequestingToSpeak { get; }
        public ReactiveProperty<bool> IsSpeaker { get; }
        public ReactiveProperty<bool> IsMuted { get; }
        public ReactiveProperty<VoiceChatParticipantCommunityRole> Role { get; }

        private VoiceChatParticipantState(string walletId, ReactiveProperty<bool> isSpeaking, ReactiveProperty<bool> isRequestingToSpeak, ReactiveProperty<bool> isSpeaker, ReactiveProperty<VoiceChatParticipantCommunityRole> role,
            ReactiveProperty<bool> isMuted)
        {
            Option<UserId> userId = UserId.New(walletId);

            Profile = userId.Has
                ? Option<Profile.CompactInfo>.Some(new Profile.CompactInfo(userId.Value))
                : Option<Profile.CompactInfo>.None;

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
                new ReactiveProperty<VoiceChatParticipantCommunityRole>(VoiceChatParticipantCommunityRole.None),
                new ReactiveProperty<bool>(false)
            );
    }

    public enum VoiceChatParticipantCommunityRole
    {
        None,
        User,
        Moderator,
        Owner,
    }

}
