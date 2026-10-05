using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Fonts
{
    public struct GetFontIntention : ILoadingIntention, IEquatable<GetFontIntention>
    {
        public string Src;

        public ConvertedFontBundle? Bundle;

        private int? hashCode;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        // Requests for the same bundle must resolve to one FontData, because they share the font assets of that bundle
        public bool Equals(GetFontIntention other) =>
            Bundle != null || other.Bundle != null
                ? StringComparer.OrdinalIgnoreCase.Equals(Bundle?.Hash, other.Bundle?.Hash)
                : this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetFontIntention other && Equals(other);

        public override int GetHashCode()
        {
            if (hashCode != null)
                return hashCode.Value;

            hashCode = Bundle is { } bundle ? StringComparer.OrdinalIgnoreCase.GetHashCode(bundle.Hash) : CommonArguments.URL.GetHashCode();
            return hashCode.Value;
        }

        public override string ToString() =>
            $"Get Font Intention: {Src} {CommonArguments.URL}";
    }

    public readonly struct ConvertedFontBundle
    {
        /// <summary>The content hash of the font file, not of the bundle. The bundle is requested by this hash.</summary>
        public readonly string Hash;

        /// <summary>True when the files[] of the scene manifest name a bundle for <see cref="Hash" />.</summary>
        public readonly bool Listed;

        public readonly AssetBundleManifestVersion Manifest;

        public readonly string SceneId;

        public ConvertedFontBundle(string hash, bool listed, AssetBundleManifestVersion manifest, string sceneId)
        {
            Hash = hash;
            Listed = listed;
            Manifest = manifest;
            SceneId = sceneId;
        }
    }
}
