using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public interface IOwnedTexture2D : IDisposable
    {
        public Texture2D Texture { get; }

        class Const : IOwnedTexture2D
        {
            public Texture2D Texture { get; }

            public Const(Texture2D texture)
            {
                Texture = texture;
            }

            public void Dispose()
            {
                Object.Destroy(Texture);
            }
        }
    }

    public class OwnedTexture2D : IOwnedTexture2D
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

        public static OwnedTexture2D NewEmptyTexture() =>
            NewTexture(Texture2D.whiteTexture, new IntPtr(-1), new IntPtr(-1));

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

                Object.Destroy(ownedTexture.texture);

                ownedTexture.texture = null!;
                ownedTexture.context = IntPtr.Zero;
                ownedTexture.handle = IntPtr.Zero;
                ownedTexture.disposed = true;
                POOL.Release(ownedTexture);

                if (context != new IntPtr(-1) && handle != new IntPtr(-1))
                    NativeMethods.TexturesFuseRelease(context, handle);
            }
        }

        public void Dispose()
        {
            Release(this);
        }
    }
}