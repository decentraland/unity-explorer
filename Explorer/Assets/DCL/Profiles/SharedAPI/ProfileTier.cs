using REnum;
using System;

namespace DCL.Profiles
{
    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(Profile.CompactInfo), "Compact")]
    [REnumField(typeof(Profile), "Full")]
    public readonly partial struct ProfileTier : IDisposable
    {
        public string UserId => Match(c => c.UserId, f => f.UserId);
        public string DisplayName => Match(c => c.DisplayName, f => f.DisplayName);

        public string ValidatedName => Match(c => c.ValidatedName, f => f.ValidatedName);

        public int Version => Match(c => 0, f => f.Version);

        public void Dispose() =>
            Match(_ => { }, full => full.Dispose());

        public static implicit operator Profile.CompactInfo(ProfileTier profileTier) =>
            profileTier.Match(c => c, f => f.Compact);

        public static implicit operator ProfileTier?(Profile? profile) =>
            profile == null ? null : FromFull(profile);

        public static implicit operator ProfileTier(Profile profile) =>
            FromFull(profile);

        public static implicit operator ProfileTier(Profile.CompactInfo profile) =>
            FromCompact(profile);
    }

    public static class ProfileTierExtensions
    {
        public static Profile? ToProfile(this ProfileTier? tier) =>
            tier == null ? null :
            tier.Value.IsFull(out Profile? fullProfile) ? fullProfile! : throw new ArgumentException($"Profile Tier is {tier.Value.GetKind()}, expected: {ProfileTier.Kind.Full}");

        public static Profile.CompactInfo? ToCompact(this ProfileTier? tier) =>
            tier;
    }
}
