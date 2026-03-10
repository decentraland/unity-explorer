using DCL.Profiles;
using DCL.Web3;
using System;

namespace DCL.Friends
{
    public class BlockedProfile
    {
        public Profile.CompactInfo Profile { get; }
        public DateTime Timestamp { get; }

        public BlockedProfile(Web3Address address,
            string name,
            bool hasClaimedName,
            string facePictureUrl,
            DateTime timestamp)
        {
            Profile = new Profile.CompactInfo(address, name, hasClaimedName, facePictureUrl);
            Timestamp = timestamp;
        }

        public Web3Address Address => Profile.Address;

        public static implicit operator Profile.CompactInfo(BlockedProfile blockedProfile) =>
            blockedProfile.Profile;
    }
}
