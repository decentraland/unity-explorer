using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    /// <summary>
    ///     Intermediate intent agnostic to the loading source.
    ///     <para>It enables support for loading GLTF Container from Asset Bundles or GLTFast</para>
    /// </summary>
    public readonly struct GetGltfContainerAssetIntention : IAssetIntention, IEquatable<GetGltfContainerAssetIntention>
    {
        public readonly string Name;
        public readonly string Hash;

        /// <summary>
        ///     Per-file deps digest from the v49+ scene asset-bundle manifest. Null/empty for legacy entries.
        ///     Disambiguates two scenes that share the same <see cref="Hash"/> but resolve different dependency closures.
        /// </summary>
        public readonly string? DepsDigest;

        public GetGltfContainerAssetIntention(string name, string hash, CancellationTokenSource cancellationTokenSource, string? depsDigest = null)
        {
            Name = name;
            Hash = hash;
            DepsDigest = depsDigest;
            CancellationTokenSource = cancellationTokenSource;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        /// <summary>
        ///     The string used to key all GLTF-container caches (in-memory pool and AssetPreLoadCache).
        ///     For v49+ entries it's <c>hash@digest</c>; for legacy entries it's the bare hash, so existing cache entries keep hitting.
        /// </summary>
        public string CacheKey => Compose(Hash, DepsDigest);

        public static string Compose(string hash, string? depsDigest) =>
            string.IsNullOrEmpty(depsDigest) ? hash : $"{hash}@{depsDigest}";

        public bool Equals(GetGltfContainerAssetIntention other)
        {
            return Name == other.Name
                   && Hash == other.Hash
                   && string.Equals(DepsDigest ?? string.Empty, other.DepsDigest ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is GetGltfContainerAssetIntention other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Hash, DepsDigest ?? string.Empty);
        }
    }
}
