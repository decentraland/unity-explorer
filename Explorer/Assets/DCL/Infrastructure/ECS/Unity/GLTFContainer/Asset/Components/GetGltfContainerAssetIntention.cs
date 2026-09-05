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
        ///     Opaque identity used to key the GLTF-container caches. Defaults to <see cref="Hash"/> for callers that
        ///     don't need extra differentiation; callers that do (e.g. v49+ AB scenes that need to disambiguate by
        ///     deps digest) build the key externally and pass it in.
        /// </summary>
        public readonly string CacheKey;

        public GetGltfContainerAssetIntention(string name, string hash, CancellationTokenSource cancellationTokenSource, string? cacheKey = null)
        {
            Name = name;
            Hash = hash;
            CacheKey = cacheKey ?? hash;
            CancellationTokenSource = cancellationTokenSource;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetGltfContainerAssetIntention other) =>
            Name == other.Name && Hash == other.Hash && CacheKey == other.CacheKey;

        public override bool Equals(object? obj) =>
            obj is GetGltfContainerAssetIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Name, Hash, CacheKey);
    }
}
