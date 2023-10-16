using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableAssetBundleManifestIntention : ILoadingIntention, IEquatable<GetWearableAssetBundleManifestIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }
        public readonly string Hash;

        public GetWearableAssetBundleManifestIntention(string hash, CommonLoadingArguments commonArguments)
        {
            Hash = hash;
            CommonArguments = commonArguments;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetWearableAssetBundleManifestIntention other) =>
            Hash == other.Hash;

        public override bool Equals(object obj) =>
            obj is GetWearableAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode() =>
            Hash.GetHashCode();
    }
}
