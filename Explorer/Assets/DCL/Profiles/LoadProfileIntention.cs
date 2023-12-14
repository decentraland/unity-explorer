using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.Profiles
{
    public struct LoadProfileIntention : ILoadingIntention, IEquatable<LoadProfileIntention>
    {
        public string ProfileId { get; }
        public int Version { get; }
        public EntityReference Entity { get; }
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public LoadProfileIntention(string profileId, int version,
            EntityReference entity,
            CommonLoadingArguments commonArguments)
        {
            ProfileId = profileId;
            Version = version;
            Entity = entity;
            CommonArguments = commonArguments;
        }

        public bool Equals(LoadProfileIntention other) =>
            ProfileId == other.ProfileId && Version == other.Version;

        public override bool Equals(object obj) =>
            obj is LoadProfileIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ProfileId, Version);

        public override string ToString() =>
            $"Get Profile: {ProfileId} {Version}";
    }
}
