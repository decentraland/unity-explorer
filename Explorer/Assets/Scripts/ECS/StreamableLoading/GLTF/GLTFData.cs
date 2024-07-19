using GLTFast;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : IAssetData
    {
        public readonly GltfImport gltfImportedData;
        public readonly GameObject containerGameObject;

        public GLTFData(GltfImport gltfImportedData, GameObject containerGameObject)
        {
            this.gltfImportedData = gltfImportedData;
            this.containerGameObject = containerGameObject;
        }

        public void Dispose()
        {
            gltfImportedData.Dispose();
            // Object.Destroy(containerGameObject);
        }
    }
}
