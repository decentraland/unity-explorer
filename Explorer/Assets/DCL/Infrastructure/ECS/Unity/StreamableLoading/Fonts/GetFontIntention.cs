using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    public struct GetFontIntention : ILoadingIntention, IEquatable<GetFontIntention>
    {
        public string Src;

        // The content hash of the font file; its converted bundle is requested by this hash.
        public string? AssetBundleHash;

        // Whether the scene's asset bundle manifest files[] name a bundle for AssetBundleHash.
        public bool AssetBundleListed;

        public AssetBundleManifestVersion? AssetBundleManifest;

        public string SceneId;

        private int? hashCode;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public bool Equals(GetFontIntention other) =>
            this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetFontIntention other && Equals(other);

        public override int GetHashCode()
        {
            if (hashCode != null)
                return hashCode.Value;

            hashCode = CommonArguments.URL.GetHashCode();
            return hashCode.Value;
        }

        public override string ToString() =>
            $"Get Font Intention: {Src} {CommonArguments.URL}";
    }
}
