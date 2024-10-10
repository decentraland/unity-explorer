using DCL.Diagnostics;
using System;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class TexturesUnzip : ITexturesUnzip
    {
        private readonly ITexturesUnzip.IOptions options;

        public TexturesUnzip(ITexturesUnzip.IOptions options)
        {
            this.options = options;

            bool result = NativeMethods.TexturesFuseInitialize();

            if (result == false)
                throw new Exception("TexturesFuseInitialize failed");
        }

        ~TexturesUnzip()
        {
            bool result = NativeMethods.TexturesFuseDispose();

            if (result == false)
                throw new Exception("TexturesFuseDispose failed");
        }

        public OwnedTexture2D TextureFromBytes(ReadOnlySpan<byte> bytes)
        {
            var texture = NewImage(bytes, out IntPtr handle);
            return OwnedTexture2D.NewTexture(texture, handle);
        }

        private Texture2D NewImage(ReadOnlySpan<byte> bytes, out IntPtr handle)
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    var result = NativeMethods.TexturesFuseProcessedImageFromMemory(
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
