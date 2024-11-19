using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Platforms;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    ///     Provides decoded and compressed textures from raw memory.
    ///     Its implementations don't guarantee thread safety.
    /// </summary>
    public interface ITexturesFuse : IDisposable
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

            [SuppressMessage("ReSharper", "InconsistentNaming")]
            NativeMethods.CMP_CustomOptions CMP_CompressOptions { get; }

            class Const : IOptions
            {
                public Const(Mode mode, NativeMethods.Swizzle swizzle, int maxSide, NativeMethods.Adjustments adjustments, NativeMethods.CMP_CustomOptions cmpCompressOptions)
                {
                    Mode = mode;
                    Swizzle = swizzle;
                    MaxSide = maxSide;
                    Adjustments = adjustments;
                    CMP_CompressOptions = cmpCompressOptions;
                }

                public Mode Mode { get; }
                public NativeMethods.Swizzle Swizzle { get; }
                public int MaxSide { get; }
                public NativeMethods.Adjustments Adjustments { get; }

                public NativeMethods.CMP_CustomOptions CMP_CompressOptions { get; }
            }
        }

        public static ITexturesFuse NewDefault(IOptions? options = null, int? workersCount = null)
        {
            var init = NativeMethods.InitOptions.NewDefault();

            if (options == null)
            {
                var mode = IPlatform.DEFAULT.Is(IPlatform.Kind.Windows)
                    ? Mode.BC7
                    : Mode.ASTC_6x6;

                options = new IOptions.Const(
                    mode,
                    NativeMethods.Swizzle.NewDefault(),
                    1024,
                    NativeMethods.Adjustments.NewEmpty(),
                    NativeMethods.CMP_CustomOptions.NewDefault()
                );
            }

            var index = 0;

            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesUnzip - NewDefault with options: {options.ToStringInfo()}");

            if (Application.platform is RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor)
                return NewManagedInstance();

            return new PooledTexturesFuse(
                () => new TexturesFuse(init, options, true)
                   .WithLog($"Worker: {++index}"),
                workersCount ?? (
                    IPlatform.DEFAULT.Is(IPlatform.Kind.Windows)
                        ? 1 // BC7 has issue with multithreading on Windows, should be solved later
                        : Environment.ProcessorCount - 1 // 1 worker is used by the main thread
                )
            );
        }

        public static ITexturesFuse NewTestInstance() =>
            NewManagedInstance();

        private static ITexturesFuse NewManagedInstance()
        {
            return new PooledTexturesFuse(
                () => new ManagedTexturesFuse(),
                3
            );
        }
    }
}

public static class OptionsExtensions
{
    public static string ToStringInfo(this ITexturesFuse.IOptions options) =>
        $"[Options, Mode: {options.Mode}, Swizzle: {options.Swizzle}, MaxSide: {options.MaxSide}, Adjustments: {options.Adjustments}]";
}
