using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    /// <summary>
    /// <inheritdoc cref="ITexturesUnzip"/>
    /// </summary>
    public class TexturesUnzip : ITexturesUnzip
    {
        private readonly ITexturesUnzip.IOptions options;
        private readonly IntPtr context;

        public TexturesUnzip(NativeMethods.InitOptions initOptions, ITexturesUnzip.IOptions options, bool debug)
        {
            this.options = options;

            initOptions = initOptions.NewWithMode(options.Mode);
            initOptions.outputMessage = debug ? OutputMessage : null;
            var result = NativeMethods.TexturesFuseInitialize(initOptions, out context);

            if (result is not NativeMethods.ImageResult.Success)
                throw new Exception($"TexturesFuseInitialize failed: {result}");
        }

        private static void OutputMessage(int format, string message)
        {
            ReportHub.Log(ReportCategory.TEXTURES, $"TexturesFuse: {message}");
        }

        ~TexturesUnzip()
        {
            var result = NativeMethods.TexturesFuseDispose(context);

            if (result is not NativeMethods.ImageResult.Success)
                throw new Exception($"TexturesFuseDispose failed: {result}");
        }

        public async UniTask<OwnedTexture2D?> TextureFromBytesAsync(ReadOnlyMemory<byte> bytes)
        {
            await UniTask.SwitchToThreadPool();
            ProcessImage(bytes, out var handle, out var pointer, out int outputLength, out NativeMethods.ImageResult result, out uint width, out uint height, out TextureFormat format);
            await UniTask.SwitchToMainThread();

            if (result is not NativeMethods.ImageResult.Success)
            {
                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseASTCImageFromMemory error during decoding: {result}");
                return null;
            }

            if (handle == IntPtr.Zero)
                throw new Exception("TexturesFuseProcessedImageFromMemory failed");

            //TODO do linear on RGBA?
            //TODO mipChain and mipmaps
            var texture = new Texture2D((int)width, (int)height, format, false, false, true);
            texture.LoadRawTextureData(pointer, outputLength);
            texture.Apply();
            return OwnedTexture2D.NewTexture(texture, context, handle);
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
            ReadOnlyMemory<byte> bytes,
            out IntPtr handle,
            out IntPtr pointer,
            out int outputLength,
            out NativeMethods.ImageResult result,
            out uint width,
            out uint height,
            out TextureFormat format
        )
        {
            unsafe
            {
                fixed (byte* ptr = bytes.Span)
                {
                    var mode = options.Mode;

                    if (mode.IsASTC())
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
