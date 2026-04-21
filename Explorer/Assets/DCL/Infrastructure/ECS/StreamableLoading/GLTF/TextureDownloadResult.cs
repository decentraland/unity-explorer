using GLTFast;
using GLTFast.Loading;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    /// Provides a mechanism to inspect the progress and result of downloading or accessing a texture from a GLTF asset.
    ///
    /// Note: This was changed from struct to class to avoid boxing both in the client and the GLTF plugin usage
    /// </summary>
    public class TextureDownloadResult : ITextureDownload
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public byte[] Data => Array.Empty<byte>();
        public string Text => string.Empty;
        public bool? IsBinary => true;

        public Texture2D? Texture { get; }

        public TextureDownloadResult(Texture2D? texture)
        {
            Texture = texture;
            Error = null!;
            Success = false;
        }

        public void Dispose()
        {
            // TODO (Maurizio) I just ported this message from deleted DisposableTexture.cs:

            // TODO: if we enable texture destruction on disposal, the external-fetched textures get destroyed before
            // the GLTF finishes loading... investigate why...
            // if (Texture != null)
            //     Object.Destroy(Texture);
        }
    }
}
