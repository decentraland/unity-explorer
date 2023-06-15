using ECS.StreamableLoading.Common.Components;
using System.Threading;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    /// <summary>
    ///     Intermediate intent agnostic to the loading source.
    ///     <para>It enables support for loading GLTF Container from Asset Bundles or GLTFast</para>
    /// </summary>
    public readonly struct GetGltfContainerAssetIntention : IAssetIntention
    {
        public readonly string Name;

        public GetGltfContainerAssetIntention(string name, CancellationTokenSource cancellationTokenSource)
        {
            Name = name;
            CancellationTokenSource = cancellationTokenSource;
        }

        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
