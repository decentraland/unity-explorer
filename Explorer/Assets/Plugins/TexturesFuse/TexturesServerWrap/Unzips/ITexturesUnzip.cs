using System;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public interface ITexturesUnzip
    {
        OwnedTexture2D TextureFromBytes(ReadOnlySpan<byte> bytes);

        interface IOptions
        {
            int MaxSide { get; }
        }
    }

    public readonly struct OwnedTexture2D : IDisposable
    {
        public readonly Texture2D Texture;
        private readonly IntPtr handle;

        public OwnedTexture2D(Texture2D texture, IntPtr handle)
        {
            this.Texture = texture;
            this.handle = handle;
        }

        public void Dispose()
        {
            NativeMethods.TexturesFuseRelease(handle);
        }
    }
}
