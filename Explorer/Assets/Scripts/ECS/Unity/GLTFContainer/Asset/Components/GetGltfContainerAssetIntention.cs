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

        public GetGltfContainerAssetIntention(string name, CancellationTokenSource cancellationTokenSource)
        {
            Name = name;
            CancellationTokenSource = cancellationTokenSource;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetGltfContainerAssetIntention other) =>
            Name == other.Name;

        public override bool Equals(object obj) =>
            obj is GetGltfContainerAssetIntention other && Equals(other);

        public override int GetHashCode() =>
            Name != null ? Name.GetHashCode() : 0;

        public override string ToString() =>
            Name;
    }
}
