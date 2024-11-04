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

        internal unsafe NativeMethods.ImageResult LoadCMPImage(
            byte* ptr,
            int ptrLength,
            NativeMethods.CMP_FORMAT cmpFormat,
            out byte* output,
            out int outputLength,
            out uint width,
            out uint height,
            out TextureFormat textureFormat,
            out IntPtr handle
        )
        {
            var result = NativeMethods.TexturesFuseCMPImageFromMemory(
                context,
                ptr,
                ptrLength,
                options.MaxSide,
                cmpFormat,
                options.CMP_CompressOptions,
                out output,
                out outputLength,
                out width,
                out height,
                out handle
            );

            switch (cmpFormat)
            {
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC7:
                    textureFormat = TextureFormat.BC7;
                    break;
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC5:
                    textureFormat = TextureFormat.BC5;
                    break;
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_Unknown:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_8888_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_8888_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_8888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ABGR_8888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_8888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGRA_8888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGB_888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGB_888_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGR_888:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RG_8_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RG_8:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_R_8_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_R_8:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_2101010:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_1010102:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ABGR_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGRA_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RG_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_R_16:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBE_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ABGR_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGRA_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RG_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_R_16F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ARGB_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ABGR_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGBA_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGRA_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RGB_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BGR_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_RG_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_R_32F:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BROTLIG:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC1:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC2:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC3:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC4:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC4_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC5_S:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC6H:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BC6H_SF:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATI1N:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATI2N:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATI2N_XY:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATI2N_DXT5:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT1:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT3:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_xGBR:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_RxBG:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_RBxG:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_xRBG:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_RGxB:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_DXT5_xGxR:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATC_RGB:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATC_RGBA_Explicit:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ATC_RGBA_Interpolated:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ASTC:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_APC:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_PVRTC:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC_RGB:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_RGB:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_SRGB:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_RGBA:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_RGBA1:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_SRGBA:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_ETC2_SRGBA1:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BINARY:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_GTC:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_BASIS:
                case NativeMethods.CMP_FORMAT.CMP_FORMAT_MAX:
                default: throw new ArgumentOutOfRangeException(nameof(cmpFormat), cmpFormat, null!);
            }

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
                        result = LoadCMPImage(
                            ptr,
                            bytes.Length,
                            NativeMethods.CMP_FORMAT.CMP_FORMAT_BC5,
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
                    else if (mode is Mode.BC7)
                    {
                        result = LoadCMPImage(
                            ptr,
                            bytes.Length,
                            NativeMethods.CMP_FORMAT.CMP_FORMAT_BC7,
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
