using GLTFast;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : IDisposable
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
