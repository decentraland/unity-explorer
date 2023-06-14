using ECS.StreamableLoading.Common.Components;
using System.Threading;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Components
{
    /// <summary>
    ///     Intermediate intent agnostic to the loading source.
    ///     <para>It enables support for loading GLTF Container from Asset Bundles or GLTFast</para>
    /// </summary>
    public readonly struct GetGltfContainerAssetIntention : IAssetIntention
    {
        public readonly string Hash;

        /// <summary>
        ///     In order to prevent reparenting overhead, GLTF container will be instantiated under the desired parent straight-away
        /// </summary>
        public readonly Transform Parent;

        public GetGltfContainerAssetIntention(string hash, CancellationTokenSource cancellationTokenSource, Transform parent)
        {
            Hash = hash;
            CancellationTokenSource = cancellationTokenSource;
            Parent = parent;
        }

        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
