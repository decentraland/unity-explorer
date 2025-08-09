using DCL.Infrastructure.ECS.StreamableLoading.AssetBundles.AssetBundleManifestHelper;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleManifestIntention : ILoadingIntention, IEquatable<GetAssetBundleManifestIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }
        public readonly string Hash;
        public IApplyAssetBundleManifestResult ApplyAssetBundleManifestResultTo;

        private GetAssetBundleManifestIntention(string hash, CommonLoadingArguments commonArguments, IApplyAssetBundleManifestResult applyAssetBundleManifestResultTo)
        {
            Hash = hash;
            CommonArguments = commonArguments;
            ApplyAssetBundleManifestResultTo = applyAssetBundleManifestResultTo;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetAssetBundleManifestIntention other) =>
            Hash == other.Hash;

        public override bool Equals(object obj) =>
            obj is GetAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode() =>
            Hash.GetHashCode();

        public static GetAssetBundleManifestIntention Create(string hash, CommonLoadingArguments commonArguments, IApplyAssetBundleManifestResult applyAssetBundleManifestResult) =>
            new (hash, commonArguments, applyAssetBundleManifestResult);
    }
}
