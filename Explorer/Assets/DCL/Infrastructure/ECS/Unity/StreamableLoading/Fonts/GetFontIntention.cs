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

        // A bundle's font assets are shared by every request for it, so requests for the same bundle resolve to one FontData
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

    /// <summary>
    ///     The converted bundle of a scene font file.
    /// </summary>
    public readonly struct ConvertedFontBundle
    {
        /// <summary>The content hash of the font file, which the bundle is requested by.</summary>
        public readonly string Hash;

        /// <summary>Whether the scene manifest's files[] name a bundle for <see cref="Hash" />.</summary>
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
