using GLTFast;
using System;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : IDisposable
    {
        public readonly GltfImport gltfImportedData;

        public GLTFData(GltfImport gltfImportedData)
        {
            this.gltfImportedData = gltfImportedData;
        }

        public void Dispose()
        {
            gltfImportedData.Dispose();
        }
    }
}
