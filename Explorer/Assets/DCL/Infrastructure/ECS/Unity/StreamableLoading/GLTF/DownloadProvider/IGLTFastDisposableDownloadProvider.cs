using DCL.Ipfs;
using GLTFast.Loading;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.GLTF
{
    public interface IGLTFastDisposableDownloadProvider : IDownloadProvider, IDisposable
    {
        /// <summary>
        ///     External files fetched during the import mapped to the content URL each resolved to;
        ///     null when the provider does not track them.
        /// </summary>
        IReadOnlyList<GltfExternalDependency>? ExternalDependencies { get; }

        void SetContentMappings(ContentDefinition[] contentMappings);
    }
}
