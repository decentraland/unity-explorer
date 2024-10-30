using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
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
        UniTask<EnumResult<IOwnedTexture2D, NativeMethods.ImageResult>> TextureFromBytesAsync(
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

            var mode = Application.platform
                is RuntimePlatform.WindowsPlayer
                or RuntimePlatform.WindowsEditor
                or RuntimePlatform.WindowsServer
                ? Mode.BC7
                : Mode.ASTC_6x6;

            var options = new IOptions.Const(mode, NativeMethods.Swizzle.NewDefault(), 1024, NativeMethods.Adjustments.NewEmpty());
            var index = 0;

            return new PooledTexturesUnzip(
                () => new TexturesUnzip(init, options, true)
                   .WithLog((++index).ToString())

                // .WithSemaphore() -
                // is not required since PooledTexturesUnzip has synchronization for the access and prevents double calling of TextureFromBytesAsync
               ,
                Environment.ProcessorCount - 1 // 1 worker is used by the main thread
            );
        }

        public static ITexturesUnzip NewTestInstance()
        {
            return new PooledTexturesUnzip(
                () => new ManagedTexturesUnzip(),
                3
            );
        }
    }
}
