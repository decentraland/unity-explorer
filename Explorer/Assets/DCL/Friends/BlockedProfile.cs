using DCL.Profiles;
using DCL.Web3;
using System;

namespace DCL.Friends
{
    public class BlockedProfile : IEquatable<BlockedProfile>
    {
        public Profile.CompactInfo Profile { get; }
        public DateTime Timestamp { get; }

        public BlockedProfile(UserId userId,
            string name,
            bool hasClaimedName,
            string facePictureUrl,
            DateTime timestamp)
        {
            Profile = new Profile.CompactInfo(userId, name, hasClaimedName, facePictureUrl);
            Timestamp = timestamp;
        }

        public Web3Address Address => Profile.Address;

        /// <summary>
        ///     Identity is the address alone: every server response builds a new instance for the same blocked user.
        /// </summary>
        public bool Equals(BlockedProfile? other) =>
            other is not null && Address.Equals(other.Address);

        public override bool Equals(object? obj) =>
            obj is BlockedProfile other && Equals(other);

        public override int GetHashCode() =>
            Address.GetHashCode();

        public static implicit operator Profile.CompactInfo(BlockedProfile blockedProfile) =>
            blockedProfile.Profile;
    }
}
