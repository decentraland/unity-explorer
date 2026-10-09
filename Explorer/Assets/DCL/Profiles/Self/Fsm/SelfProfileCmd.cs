using REnum;
using System;
using System.Runtime.CompilerServices;

namespace DCL.Profiles.Self
{
    public readonly struct DeployCmd : IEquatable<DeployCmd>
    {
        public readonly UserId Address;
        public readonly Profile Profile;

        /// <summary>The version the profile is deployed as.</summary>
        public readonly int Version;

        /// <summary>The profile is reported as saved without being sent to the catalyst.</summary>
        public readonly bool LocalOnly;

        public DeployCmd(UserId address, Profile profile, int version, bool localOnly = false)
        {
            Address = address;
            Profile = profile;
            Version = version;
            LocalOnly = localOnly;
        }

        public bool Equals(DeployCmd other) =>
            Address.Equals(other.Address) && ReferenceEquals(Profile, other.Profile) && Version == other.Version && LocalOnly == other.LocalOnly;

        public override bool Equals(object? obj) =>
            obj is DeployCmd other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Address, RuntimeHelpers.GetHashCode(Profile), Version, LocalOnly);

        public override string ToString() =>
            LocalOnly ? $"{Address.Value} v{Version} local only" : $"{Address.Value} v{Version}";
    }

    /// <summary>
    ///     Intents the self-profile update emits for the executor. <c>None</c> = nothing to do; <c>Ignore</c> = the message did not
    ///     apply, the payload says why; <c>ResetLocalState</c> = drop equipped wearables, emotes and owned-NFT registries;
    ///     <c>Fetch</c> = read the profile of the address; <c>Publish</c> = make the profile the local truth;
    ///     <c>Deploy</c> = send the profile to the catalyst and re-read it; <c>Batch</c> = perform the commands in order.
    /// </summary>
    [REnum(EnumUnderlyingType.Byte)]
    [REnumFieldEmpty("None")]
    [REnumField(typeof(string), "Ignore")]
    [REnumFieldEmpty("ResetLocalState")]
    [REnumField(typeof(UserId), "Fetch")]
    [REnumField(typeof(Profile), "Publish")]
    [REnumField(typeof(DeployCmd), "Deploy")]
    [REnumField(typeof(SelfProfileCmd[]), "Batch")]
    public readonly partial struct SelfProfileCmd { }
}
