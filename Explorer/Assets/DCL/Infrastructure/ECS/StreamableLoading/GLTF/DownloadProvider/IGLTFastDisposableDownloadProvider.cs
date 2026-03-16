// GLTFast forces usage of Task that is not compatible with WebGL

using DCL.Ipfs;
using GLTFast.Loading;
using System;

#if !UNITY_WEBGL
namespace ECS.StreamableLoading.GLTF
{
    public interface IGLTFastDisposableDownloadProvider : IDownloadProvider, IDisposable
    {
        void SetContentMappings(ContentDefinition[] contentMappings);
    }
}
#endif
