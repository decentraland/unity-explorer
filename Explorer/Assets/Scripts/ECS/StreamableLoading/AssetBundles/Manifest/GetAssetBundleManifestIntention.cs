using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    public struct GetAssetBundleManifestIntention : ILoadingIntention, IEquatable<GetAssetBundleManifestIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly string SceneId;

        public GetAssetBundleManifestIntention(string sceneId) : this()
        {
            SceneId = sceneId;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.cancellationTokenSource;

        public bool Equals(GetAssetBundleManifestIntention other) =>
            SceneId == other.SceneId;

        public override bool Equals(object obj) =>
            obj is GetAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode() =>
            SceneId.GetHashCode();
    }
}
