using GLTFast;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : IAssetData, IStreamableRefCountData
    {
        private readonly GltfImport gltfImportData;

        public GLTFData(GltfImport gltfImportData, GameObject containerGameObject)
        {
            this.gltfImportData = gltfImportData;
            this.MainAsset = containerGameObject;
        }

        public void Dispose()
        {
            gltfImportData.Dispose();
        }

        public GameObject MainAsset { get; }
        public AnimationClip[]? AnimationClips => gltfImportData.GetAnimationClips();
    }
}
