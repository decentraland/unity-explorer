#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Runtime.InteropServices;

namespace DCL.VideoPlayback
{
    /// <summary>
    /// Framing for the decoder's video output. Every frame arrives as a complete
    /// 32-bit BMP, so each one carries its own dimensions: the stream stays
    /// parseable across resolution changes and re-synchronises on the next
    /// header if a frame is ever truncated.
    /// </summary>
    internal static class BmpFrame
    {
        public const int HEADER_BYTES = 54;
        public const int BYTES_PER_PIXEL = 4;

        private const int MIN_DIB_HEADER_BYTES = 40;
        private const int MAX_DIMENSION = 16384;

        public readonly struct Header
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool TopDown;
            public readonly int PixelOffset;
            public readonly int PixelBytes;

            public Header(int width, int height, bool topDown, int pixelOffset)
            {
                Width = width;
                Height = height;
                TopDown = topDown;
                PixelOffset = pixelOffset;
                PixelBytes = width * BYTES_PER_PIXEL * height;
            }
        }

        /// <summary>Parses the 14-byte file header plus the leading 40 DIB bytes; <paramref name="error"/> is empty on success.</summary>
        public static bool TryParseHeader(byte[] data, out Header header, out string error)
        {
            header = default;
            error = string.Empty;

            if (data.Length < HEADER_BYTES)
            {
                error = "bmp header truncated";
                return false;
            }

            if (data[0] != (byte)'B' || data[1] != (byte)'M')
            {
                error = "bmp signature missing";
                return false;
            }

            int pixelOffset = BitConverter.ToInt32(data, 10);
            int dibSize = BitConverter.ToInt32(data, 14);
            int width = BitConverter.ToInt32(data, 18);
            int rawHeight = BitConverter.ToInt32(data, 22);
            int bitsPerPixel = BitConverter.ToUInt16(data, 28);

            if (dibSize < MIN_DIB_HEADER_BYTES || pixelOffset < 14 + dibSize)
            {
                error = $"bmp dib header unsupported (size {dibSize}, pixel offset {pixelOffset})";
                return false;
            }

            if (bitsPerPixel != BYTES_PER_PIXEL * 8)
            {
                error = $"bmp depth {bitsPerPixel} unsupported, expected 32";
                return false;
            }

            int height = Math.Abs(rawHeight);

            if (width <= 0 || height <= 0 || width > MAX_DIMENSION || height > MAX_DIMENSION)
            {
                error = $"bmp dimensions {width}x{rawHeight} out of range";
                return false;
            }

            header = new Header(width, height, rawHeight < 0, pixelOffset);
            return true;
        }

        /// <summary>
        /// Converts the BGRA pixel block into <paramref name="rgba"/> laid out
        /// bottom-up, which is the row order <c>Texture2D.LoadRawTextureData</c>
        /// expects. Bottom-up BMPs copy straight through; top-down ones are
        /// flipped on the way.
        /// </summary>
        public static void ConvertToRgba(byte[] bgra, in Header header, byte[] rgba)
        {
            CopyRows(bgra, header, rgba, true);
        }

        /// <summary>Copies the BGRA pixel block into <paramref name="dst"/> bottom-up, keeping the channel order.</summary>
        public static void CopyBgra(byte[] bgra, in Header header, byte[] dst)
        {
            CopyRows(bgra, header, dst, false);
        }

        private static void CopyRows(byte[] bgra, in Header header, byte[] dst, bool swizzleToRgba)
        {
            if (bgra.Length < header.PixelBytes) throw new ArgumentException("pixel block truncated", nameof(bgra));
            if (dst.Length < header.PixelBytes) throw new ArgumentException("destination too small", nameof(dst));

            int rowPixels = header.Width;
            ReadOnlySpan<uint> src = MemoryMarshal.Cast<byte, uint>(new ReadOnlySpan<byte>(bgra, 0, header.PixelBytes));
            Span<uint> target = MemoryMarshal.Cast<byte, uint>(new Span<byte>(dst, 0, header.PixelBytes));

            for (int row = 0; row < header.Height; row++)
            {
                int srcRow = header.TopDown ? header.Height - 1 - row : row;
                ReadOnlySpan<uint> srcLine = src.Slice(srcRow * rowPixels, rowPixels);
                Span<uint> dstLine = target.Slice(row * rowPixels, rowPixels);

                if (!swizzleToRgba)
                {
                    srcLine.CopyTo(dstLine);
                    continue;
                }

                for (int x = 0; x < rowPixels; x++)
                {
                    uint pixel = srcLine[x];
                    dstLine[x] = (pixel & 0xFF00FF00u) | ((pixel & 0x00FF0000u) >> 16) | ((pixel & 0x000000FFu) << 16);
                }
            }
        }
    }
}
#endif
