using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Pool;

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

    public class OwnedTexture2D : IDisposable
    {
        private static readonly ObjectPool<OwnedTexture2D> POOL = new (() => new OwnedTexture2D());

        private Texture2D texture;
        private IntPtr handle;
        private bool disposed;

        public Texture2D Texture
        {
            get
            {
                if (disposed)
                {
                    ReportHub.LogError(ReportCategory.TEXTURES, "Attempt to access to disposed texture");
                    return Texture2D.grayTexture!;
                }

                return texture;
            }
        }

#pragma warning disable CS8618
        private OwnedTexture2D() { }
#pragma warning restore CS8618

        public static OwnedTexture2D NewTexture(Texture2D texture, IntPtr handle)
        {
            lock (POOL)
            {
                var output = POOL.Get()!;
                output.texture = texture;
                output.handle = handle;
                output.disposed = false;
                return output;
            }
        }

        private static void Release(OwnedTexture2D ownedTexture)
        {
            lock (POOL)
            {
                var handle = ownedTexture.handle;
                //TODO destroy unity texture object
                ownedTexture.texture = null!;
                ownedTexture.handle = IntPtr.Zero;
                ownedTexture.disposed = true;
                POOL.Release(ownedTexture);
                NativeMethods.TexturesFuseRelease(handle);
            }
        }

        public void Dispose()
        {
            Release(this);
        }
    }
}
