using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.Profiles
{
    public struct GetProfileIntention : ILoadingIntention, IEquatable<GetProfileIntention>
    {
        public string ProfileId { get; }
        public int Version { get; }
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public GetProfileIntention(string profileId, int version)
        {
            ProfileId = profileId;
            Version = version;
            CommonArguments = new CommonLoadingArguments(FakeUrl(profileId, version));
        }

        public bool Equals(GetProfileIntention other) =>
            ProfileId == other.ProfileId && Version == other.Version;

        public override bool Equals(object obj) =>
            obj is GetProfileIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(ProfileId, Version);

        public override string ToString() =>
            $"Get Profile: {ProfileId} - {Version}";

        public static string FakeUrl(string profileId, int version) =>
            $"FAKE_URL_{profileId}_{version}";
    }
}
