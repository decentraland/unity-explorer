using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    /// <summary>
    ///     Intermediate intent agnostic to the loading source.
    ///     <para>It enables support for loading GLTF Container from Asset Bundles or GLTFast</para>
    /// </summary>
    public readonly struct GetGltfContainerAssetIntention : IEquatable<GetGltfContainerAssetIntention>, IAssetIntention
    {
        public readonly string Name;
        public readonly string Hash;


        public GetGltfContainerAssetIntention(string name, string hash,CancellationTokenSource cancellationTokenSource)
        {
            Name = name;
            Hash = hash;
            CancellationTokenSource = cancellationTokenSource;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetGltfContainerAssetIntention other) =>
            Name == other.Name && Hash == other.Hash;

        public override bool Equals(object? obj) =>
            obj is GetGltfContainerAssetIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Name, Hash);
    }
}
