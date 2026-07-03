using DCL.Ipfs;
using GLTFast.Loading;
using System;

namespace ECS.StreamableLoading.GLTF
{
    public interface IGLTFastDisposableDownloadProvider : IDownloadProvider, IDisposable
    {
        void SetContentMappings(ContentDefinition[] contentMappings);
    }
}
