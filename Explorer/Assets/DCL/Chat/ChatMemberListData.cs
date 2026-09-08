using DCL.Profiles;

namespace DCL.Chat
{
    /// <summary>
    ///     A subset of a Profile, stores only the necessary data to be presented by the view.
    /// </summary>
    public readonly struct ChatMemberListData
    {
        public readonly Profile.CompactInfo Profile;
        public readonly ChatMemberConnectionStatus ConnectionStatus;

        /// <summary>
        ///     False for a wallet placeholder: the participant's profile could not be resolved, so no profile actions apply to the row.
        /// </summary>
        public readonly bool HasProfile;

        public string Name => Profile.ValidatedName;

        public ChatMemberListData(Profile.CompactInfo profile, ChatMemberConnectionStatus connectionStatus, bool hasProfile)
        {
            Profile = profile;
            ConnectionStatus = connectionStatus;
            HasProfile = hasProfile;
        }
    }
}
