using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleManifestIntention : ILoadingIntention, IEquatable<GetAssetBundleManifestIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }
        public readonly string Hash;

        private GetAssetBundleManifestIntention(string hash, CommonLoadingArguments commonArguments)
        {
            Hash = hash;
            CommonArguments = commonArguments;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetAssetBundleManifestIntention other) =>
            Hash == other.Hash;

        public override bool Equals(object obj) =>
            obj is GetAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode() =>
            Hash.GetHashCode();

        public static GetAssetBundleManifestIntention Create(string hash, CommonLoadingArguments commonArguments) =>
            new (hash, commonArguments);
    }
}
