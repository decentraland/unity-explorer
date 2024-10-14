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

        public OwnedTexture2D TextureFromBytes(ReadOnlySpan<byte> bytes)
        {
            var texture = NewImage(bytes, out IntPtr handle);
            return OwnedTexture2D.NewTexture(texture, context, handle);
        }

        private Texture2D NewImage(ReadOnlySpan<byte> bytes, out IntPtr handle)
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var mode = options.Mode;

                    //TODO remove this switch to avoid additional dispatching overhead in prod
                    if (mode.IsASTC())
                    {
                        var result = NativeMethods.TexturesFuseASTCImageFromMemory(
                            context,
                            options.Swizzle,
                            ptr,
                            bytes.Length,
                            options.MaxSide,
                            options.Adjustments,
                            out byte* output,
                            out int outputLength,
                            out uint width,
                            out uint height,
                            out handle
                        );

                        if (result is not NativeMethods.ImageResult.Success)
                        {
                            ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseASTCImageFromMemory error during decoding: {result}");
                            return Texture2D.whiteTexture; //TODO result type
                        }

                        if (handle == IntPtr.Zero)
                            throw new Exception("TexturesFuseProcessedImageFromMemory failed");

                        //TODO mipChain and mipmaps
                        var texture = new Texture2D((int)width, (int)height, mode.AsASTCTextureFormatOrFatalError(), false, false, true);
                        texture.LoadRawTextureData(new IntPtr(output), outputLength);
                        texture.Apply();

                        return texture;
                    }

                    if (options.Mode is Mode.RGB)
                    {
                        var result = NativeMethods.TexturesFuseProcessedImageFromMemory(
                            context,
                            ptr,
                            bytes.Length,
                            options.MaxSide,
                            out byte* output,
                            out uint width,
                            out uint height,
                            out uint bitsPerPixel,
                            out NativeMethods.FreeImageColorType colorType,
                            out handle
                        );

                        if (result is not NativeMethods.ImageResult.Success)
                        {
                            ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseProcessedImageFromMemory error during decoding: {result}");
                            return Texture2D.whiteTexture; //TODO result type
                        }

                        var format = FormatFromBpp(colorType, bitsPerPixel);

                        if (format.HasValue == false)
                        {
                            ReportHub.LogError(ReportCategory.TEXTURES, $"Unsupported format on decoding image from: color type {colorType}, bpp {bitsPerPixel}");
                            return Texture2D.whiteTexture; //TODO result type
                        }

                        if (handle == IntPtr.Zero)
                            throw new Exception("TexturesFuseProcessedImageFromMemory failed");

                        var texture = new Texture2D((int)width, (int)height, format.Value, false);
                        uint length = width * height * (bitsPerPixel / 8);
                        texture.LoadRawTextureData(new IntPtr(output), (int)length);
                        texture.Apply();

                        return texture;
                    }

                    throw new ArgumentOutOfRangeException();
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
