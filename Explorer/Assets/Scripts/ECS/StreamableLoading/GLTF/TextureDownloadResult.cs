using GLTFast;
using GLTFast.Loading;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public struct TextureDownloadResult : ITextureDownload
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public byte[] Data => Array.Empty<byte>();
        public string Text => string.Empty;
        public bool? IsBinary => true;
        private readonly IDisposableTexture texture;

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
