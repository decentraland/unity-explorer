using AOT;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;
using UnityEngine;
using Result = Utility.Types.EnumResult<
    Plugins.TexturesFuse.TexturesServerWrap.Unzips.IOwnedTexture2D,
    Plugins.TexturesFuse.TexturesServerWrap.NativeMethods.ImageResult
>;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    /// <inheritdoc cref="ITexturesUnzip"/>
    /// </summary>
    public class TexturesUnzip : ITexturesUnzip
    {
        private readonly ITexturesUnzip.IOptions options;
        private readonly IntPtr context;
        private bool disposed;

        public TexturesUnzip(NativeMethods.InitOptions initOptions, ITexturesUnzip.IOptions options, bool debug)
        {
            this.options = options;

            initOptions = initOptions.NewWithMode(options.Mode);
            initOptions.outputMessage = debug ? OutputMessage : null;
            var result = NativeMethods.TexturesFuseInitialize(initOptions, out context);

            if (result is not NativeMethods.ImageResult.Success)
                throw new Exception($"TexturesFuseInitialize failed: {result}");
        }

        [MonoPInvokeCallback(typeof(NativeMethods.OutputMessageDelegate))]
        private static void OutputMessage(int format, string message)
        {
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesFuse: {message}");
        }

        private void ReleaseUnmanagedResources()
        {
            var result = NativeMethods.TexturesFuseDispose(context);

            if (result is not NativeMethods.ImageResult.Success)
                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseDispose failed: {result}");
        }

        ~TexturesUnzip()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public async UniTask<Result> TextureFromBytesAsync(
            IntPtr bytes,
            int bytesLength,
            TextureType type,
            CancellationToken token
        )
        {
            if (Result.TryErrorIfCancelled(token, out var errorResult))
                return errorResult;

            await UniTask.SwitchToThreadPool();

            if (Result.TryErrorIfCancelled(token, out errorResult))
                return errorResult;

            ProcessImage(bytes, bytesLength, type, out var handle, out var pointer, out int outputLength, out NativeMethods.ImageResult result, out uint width, out uint height, out bool linear, out TextureFormat format);
            await UniTask.SwitchToMainThread();

            if (result is NativeMethods.ImageResult.Success && token.IsCancellationRequested)
            {
                NativeMethods.TexturesFuseRelease(context, handle);
                if (Result.TryErrorIfCancelled(token, out errorResult))
                    return errorResult;
            }

            if (result is not NativeMethods.ImageResult.Success)
            {
                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseASTCImageFromMemory error during decoding: {result}");
                return Result.ErrorResult(result, string.Empty);
            }

            if (handle == IntPtr.Zero)
                throw new Exception("TexturesFuseProcessedImageFromMemory failed");

            //TODO mipChain and mipmaps
            var texture = new Texture2D((int)width, (int)height, format, false, linear, true);
            texture.LoadRawTextureData(pointer, outputLength);
            texture.Apply();

            return Result.SuccessResult(
                OwnedTexture2D.NewTexture(texture, context, handle)
            );
        }

        internal unsafe NativeMethods.ImageResult LoadBC5Image(
            byte* ptr,
            int ptrLength,
            out byte* output,
            out int outputLength,
            out uint width,
            out uint height,
            out TextureFormat textureFormat,
            out IntPtr handle
        )
        {
            var result = NativeMethods.TexturesFuseBC5ImageFromMemory(
                context,
                ptr,
                ptrLength,
                options.MaxSide,
                out output,
                out outputLength,
                out width,
                out height,
                out handle
            );

            textureFormat = TextureFormat.BC5;

            return result;
        }

        internal unsafe NativeMethods.ImageResult LoadASTCImage(
            byte* ptr,
            int ptrLength,
            out byte* output,
            out int outputLength,
            out uint width,
            out uint height,
            out TextureFormat textureFormat,
            out IntPtr handle
        )
        {
            var result = NativeMethods.TexturesFuseASTCImageFromMemory(
                context,
                options.Swizzle,
                ptr,
                ptrLength,
                options.MaxSide,
                options.Adjustments,
                out output,
                out outputLength,
                out width,
                out height,
                out handle
            );

            textureFormat = options.Mode.AsASTCTextureFormatOrFatalError();

            return result;
        }

        internal unsafe NativeMethods.ImageResult LoadRGBAImage(
            byte* ptr,
            int ptrLength,
            out byte* output,
            out int outputLength,
            out uint width,
            out uint height,
            out TextureFormat textureFormat,
            out IntPtr handle
        )
        {
            var result = NativeMethods.TexturesFuseProcessedImageFromMemory(
                context,
                ptr,
                ptrLength,
                options.MaxSide,
                out output,
                out width,
                out height,
                out uint bitsPerPixel,
                out NativeMethods.FreeImageColorType colorType,
                out handle
            );

            outputLength = (int)(width * height * (bitsPerPixel / 8));
            var format = FormatFromBpp(colorType, bitsPerPixel);

            if (format.HasValue == false)
            {
                ReportHub.LogError(ReportCategory.TEXTURES, $"Unsupported format on decoding image from: color type {colorType}, bpp {bitsPerPixel}");
                textureFormat = TextureFormat.R8; //Unknown format
                return NativeMethods.ImageResult.ErrorUnknownImageFormat;
            }

            textureFormat = format.Value;
            return result;
        }

        private void ProcessImage(
            IntPtr bytes,
            int bytesLength,
            TextureType type,
            out IntPtr handle,
            out IntPtr pointer,
            out int outputLength,
            out NativeMethods.ImageResult result,
            out uint width,
            out uint height,
            out bool linear,
            out TextureFormat format
        )
        {
            unsafe
            {
                ProcessImage(
                    new ReadOnlySpan<byte>(bytes.ToPointer()!, bytesLength),
                    type,
                    out handle,
                    out pointer,
                    out outputLength,
                    out result,
                    out width,
                    out height,
                    out linear,
                    out format
                );
            }
        }

        private void ProcessImage(
            ReadOnlySpan<byte> bytes,
            TextureType type,
            out IntPtr handle,
            out IntPtr pointer,
            out int outputLength,
            out NativeMethods.ImageResult result,
            out uint width,
            out uint height,
            out bool linear,
            out TextureFormat format
        )
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var mode = options.Mode;

                    if (type is TextureType.NormalMap)
                    {
                        result = LoadBC5Image(
                            ptr,
                            bytes.Length,
                            out byte* output,
                            out outputLength,
                            out width,
                            out height,
                            out format,
                            out handle
                        );

                        pointer = new IntPtr(output);
                        linear = true;
                    }
                    else if (mode.IsASTC())
                    {
                        result = LoadASTCImage(
                            ptr,
                            bytes.Length,
                            out byte* output,
                            out outputLength,
                            out width,
                            out height,
                            out format,
                            out handle
                        );

                        pointer = new IntPtr(output);
                        linear = false;
                    }
                    else if (mode is Mode.RGB)
                    {
                        result = LoadRGBAImage(
                            ptr,
                            bytes.Length,
                            out byte* output,
                            out outputLength,
                            out width,
                            out height,
                            out format,
                            out handle
                        );

                        pointer = new IntPtr(output);
                        linear = false;
                    }
                    else
                        throw new Exception($"Unsupported mode: {mode}");
                }
            }
        }

        private static TextureFormat? FormatFromBpp(NativeMethods.FreeImageColorType colorType, uint bpp)
        {
            if (colorType == NativeMethods.FreeImageColorType.RGB)
            {
                switch (bpp)
                {
                    case 24:
                        return TextureFormat.RGB24;

                    // Why it's considered as RGB and not as RGBA?
                    case 32:
                        return TextureFormat.RGBA32;
                }
            }

            if (colorType == NativeMethods.FreeImageColorType.Rgbalpha)
            {
                switch (bpp)
                {
                    case 32:
                        return TextureFormat.RGBA32;
                    case 128:
                        return TextureFormat.RGBAFloat;
                }
            }

            return null;
        }
    }
}
