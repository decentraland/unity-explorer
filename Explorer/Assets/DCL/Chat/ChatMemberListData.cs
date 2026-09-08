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

        public string Name => Profile.ValidatedName;

        public ChatMemberListData(Profile.CompactInfo profile, ChatMemberConnectionStatus connectionStatus)
        {
            Profile = profile;
            ConnectionStatus = connectionStatus;
        }
    }
}
