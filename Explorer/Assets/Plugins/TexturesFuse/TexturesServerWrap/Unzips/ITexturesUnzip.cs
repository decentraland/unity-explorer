using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    ///     Provides decoded and compressed textures from raw memory.
    ///     Its implementations don't guarantee thread safety.
    /// </summary>
    public interface ITexturesUnzip
    {
        OwnedTexture2D TextureFromBytes(ReadOnlySpan<byte> bytes);

        interface IOptions
        {
            Mode Mode { get; }

            NativeMethods.Swizzle Swizzle { get; }

            int MaxSide { get; }

            NativeMethods.Adjustments Adjustments { get; }
        }
    }

    public class OwnedTexture2D : IDisposable
    {
        private static readonly ObjectPool<OwnedTexture2D> POOL = new (() => new OwnedTexture2D());

        private Texture2D texture;
        private IntPtr context;
        private IntPtr handle;
        private bool disposed;

        public Texture2D Texture
        {
            get
            {
                if (disposed)
                {
                    ReportHub.LogError(ReportCategory.TEXTURES, "Attempt to access to released texture");
                    return Texture2D.grayTexture!;
                }

                return texture;
            }
        }

#pragma warning disable CS8618
        private OwnedTexture2D() { }
#pragma warning restore CS8618

        public static OwnedTexture2D NewTexture(Texture2D texture, IntPtr context, IntPtr handle)
        {
            lock (POOL)
            {
                var output = POOL.Get()!;
                output.texture = texture;
                output.context = context;
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
                var context = ownedTexture.context;

                if (context == IntPtr.Zero || handle == IntPtr.Zero)
                {
                    ReportHub.LogError(ReportCategory.TEXTURES, "Attempt to release already released texture");
                    return;
                }

                //TODO destroy unity texture object
                ownedTexture.texture = null!;
                ownedTexture.context = IntPtr.Zero;
                ownedTexture.handle = IntPtr.Zero;
                ownedTexture.disposed = true;
                POOL.Release(ownedTexture);
                NativeMethods.TexturesFuseRelease(context, handle);
            }
        }

        public void Dispose()
        {
            Release(this);
        }
    }
}
