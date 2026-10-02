using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    public struct GetFontIntention : ILoadingIntention, IEquatable<GetFontIntention>
    {
        public string Src;

        // Set for a scene font file with a usable asset bundle manifest; the font loads only from its converted bundle.
        public string? AssetBundleHash;

        // The manifest's files[] name a bundle for AssetBundleHash. Local scene development never reads files[], so there a bundle is tried unlisted.
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
