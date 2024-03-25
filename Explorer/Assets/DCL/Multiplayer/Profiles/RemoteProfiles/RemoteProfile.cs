using DCL.Profiles;

namespace DCL.Multiplayer.Profiles.RemoteProfiles
{
    public readonly struct RemoteProfile
    {
        public readonly Profile Profile;
        public readonly string WalletId;

        public RemoteProfile(Profile profile, string walletId)
        {
            Profile = profile;
            WalletId = walletId;
        }
    }
}
