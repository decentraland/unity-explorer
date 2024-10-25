using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    ///     Provides decoded and compressed textures from raw memory.
    ///     Its implementations don't guarantee thread safety.
    /// </summary>
    public interface ITexturesUnzip : IDisposable
    {
        /// <param name="bytes">Pointer to the array of encoded data, client guarantees the pointer will be valid for the whole duration of Task.</param>
        /// <param name="bytesLength">Length of encoded data.</param>
        /// <param name="type">Desired type that result will be consumed.</param>
        /// <param name="token">Cancellation Token to cancel operation.</param>
        UniTask<EnumResult<OwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(
            IntPtr bytes,
            int bytesLength,
            TextureType type,
            CancellationToken token
        );

        interface IOptions
        {
            Mode Mode { get; }

            NativeMethods.Swizzle Swizzle { get; }

            int MaxSide { get; }

            NativeMethods.Adjustments Adjustments { get; }

            class Const : IOptions
            {
                public Const(Mode mode, NativeMethods.Swizzle swizzle, int maxSide, NativeMethods.Adjustments adjustments)
                {
                    Mode = mode;
                    Swizzle = swizzle;
                    MaxSide = maxSide;
                    Adjustments = adjustments;
                }

                public Mode Mode { get; }
                public NativeMethods.Swizzle Swizzle { get; }
                public int MaxSide { get; }
                public NativeMethods.Adjustments Adjustments { get; }
            }
        }

        public static ITexturesUnzip NewDefault()
        {
            var init = NativeMethods.InitOptions.NewDefault();
            var options = new IOptions.Const(Mode.ASTC_6x6, NativeMethods.Swizzle.NewDefault(), 1024, NativeMethods.Adjustments.NewEmpty());
            var index = 0;

            return new PooledTexturesUnzip(
                () => new TexturesUnzip(init, options, true)
                   .WithLog((++index).ToString())

                // .WithSemaphore() -
                // is not required since PooledTexturesUnzip has synchronization for the access and prevents double calling of TextureFromBytesAsync
               ,
                3
            );
        }
    }

    public static class TexturesUnzipExtensions
    {
        private struct HandleScope : IDisposable
        {
            private GCHandle handle;

            public IntPtr Addr => handle.AddrOfPinnedObject();

            public HandleScope(object obj)
            {
                handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            }

            public void Dispose()
            {
                handle.Free();
            }
        }

        public static async UniTask<EnumResult<OwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(
            this ITexturesUnzip unzip,
            byte[] bytes,
            TextureType type,
            CancellationToken token
        )
        {
            using var pinned = new HandleScope(bytes);
            var result = await unzip.TextureFromBytesAsync(pinned.Addr, bytes.Length, type, token);
            return result;
        }

        public static SemaphoreTexturesUnzip WithSemaphore(this ITexturesUnzip texturesUnzip) =>
            new (texturesUnzip);

        public static LogTexturesUnzip WithLog(this ITexturesUnzip texturesUnzip, string prefix) =>
            new (texturesUnzip, prefix);
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

                //TODO destroy unity texture object
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
