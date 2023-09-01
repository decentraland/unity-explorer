using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableAssetBundleManifestIntention : ILoadingIntention, IEquatable<GetWearableAssetBundleManifestIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }
        public string Hash;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetWearableAssetBundleManifestIntention other) =>
            Hash == other.Hash && CommonArguments.Equals(other.CommonArguments);

        public override bool Equals(object obj) =>
            obj is GetWearableAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Hash, CommonArguments);
    }
}
