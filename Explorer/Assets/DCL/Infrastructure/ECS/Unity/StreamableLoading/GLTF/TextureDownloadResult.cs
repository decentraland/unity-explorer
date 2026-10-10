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
        private readonly DisposableTexture texture;

        public TextureDownloadResult(Texture2D? texture)
        {
            this.texture = new DisposableTexture { Texture = texture };
            Error = null!;
            Success = false;
        }

        public IDisposableTexture GetTexture(bool forceSampleLinear) =>
            texture;

        public void Dispose() => texture.Dispose();
    }
}
