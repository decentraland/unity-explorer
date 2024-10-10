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

        public TexturesUnzip(ITexturesUnzip.IOptions options)
        {
            this.options = options;

            bool result = NativeMethods.TexturesFuseInitialize(out context);

            if (result == false)
                throw new Exception("TexturesFuseInitialize failed");
        }

        ~TexturesUnzip()
        {
            bool result = NativeMethods.TexturesFuseDispose(context);

            if (result == false)
                throw new Exception("TexturesFuseDispose failed");
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
                    //TODO remove this switch to avoid additional dispatching overhead in prod
                    switch (options.Mode)
                    {
                        case ITexturesUnzip.Mode.RGB:
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
                        case ITexturesUnzip.Mode.ASTC:
                            result = NativeMethods.TexturesFuseASTCImageFromMemory(
                                context,
                                ptr,
                                bytes.Length,
                                options.MaxSide,
                                out output,
                                out int outputLength,
                                out width,
                                out height,
                                out handle
                            );

                            if (result is not NativeMethods.ImageResult.Success)
                            {
                                ReportHub.LogError(ReportCategory.TEXTURES, $"TexturesFuseASTCImageFromMemory error during decoding: {result}");
                                return Texture2D.whiteTexture; //TODO result type
                            }

                            if (handle == IntPtr.Zero)
                                throw new Exception("TexturesFuseProcessedImageFromMemory failed");

                            //TODO block size
                            //TODO mipChain and mipmaps
                            texture = new Texture2D((int)width, (int)height, TextureFormat.ASTC_4x4, false);
                            texture.LoadRawTextureData(new IntPtr(output), outputLength);
                            texture.Apply();

                            return texture;

                        default: throw new ArgumentOutOfRangeException();
                    }
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
                }
            }

            return null;
        }
    }
}
