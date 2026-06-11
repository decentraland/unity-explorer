using GLTFast;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    /// Represents a texture resource that can be disposed of when no longer needed.
    /// 
    /// Notes:
    /// - Known issue where destruction of texture on disposal is performed before the GLTF finishes loading. Pending investigation.
    /// - This was changed from struct to class to avoid boxing both in the client and the GLTF plugin usage
    /// </summary>
    public class DisposableTexture : IDisposableTexture
    {
        public Texture2D? Texture { get; set; }

        public void Dispose()
        {
            // TODO: if we enable texture destruction on disposal, the external-fetched textures get destroyed before
            // the GLTF finishes loading... investigate why...
            // if (Texture != null)
            //     Object.Destroy(Texture);
        }
    }
}
