using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace DCL.WebRequests
{
    /// <summary>
    ///     CPU decoder for palette-indexed (color type 3) PNGs. Unity's LoadImage on Linux reports
    ///     success for these but never writes the decoded pixels — the texture keeps whatever memory
    ///     backed its allocation — so they must be expanded to RGBA here before reaching the engine.
    ///     Supports non-interlaced images at bit depths 1/2/4/8; anything else is left to the caller.
    /// </summary>
    public static class PalettedPng
    {
        public static bool IsPalettedPng(byte[] data) =>
            data.Length > 29
            && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[25] == 3;

        public static bool TryDecode(byte[] data, bool linear, out Texture2D texture)
        {
            texture = null;

            try { texture = Decode(data, linear); }
            catch (Exception) { return false; }

            return texture != null;
        }

        private static Texture2D Decode(byte[] data, bool linear)
        {
            int width = ReadBE(data, 16);
            int height = ReadBE(data, 20);
            int depth = data[24];

            if (data[25] != 3 || data[28] != 0 || depth > 8 || 8 % depth != 0 || width <= 0 || height <= 0)
                return null;

            byte[] palette = null;
            byte[] alpha = null;
            using var idat = new MemoryStream();

            for (int pos = 8; pos + 8 <= data.Length;)
            {
                int len = ReadBE(data, pos);
                int payload = pos + 8;

                if (payload + len > data.Length) return null;

                uint type = (uint)ReadBE(data, pos + 4);

                if (type == 0x504C5445) // PLTE
                {
                    palette = new byte[len];
                    Array.Copy(data, payload, palette, 0, len);
                }
                else if (type == 0x74524E53) // tRNS
                {
                    alpha = new byte[len];
                    Array.Copy(data, payload, alpha, 0, len);
                }
                else if (type == 0x49444154) // IDAT
                    idat.Write(data, payload, len);
                else if (type == 0x49454E44) // IEND
                    break;

                pos = payload + len + 4;
            }

            if (palette == null || idat.Length < 3) return null;

            // The IDAT payload is a zlib stream: a 2-byte header, then raw deflate data.
            idat.Position = 2;
            int stride = ((width * depth) + 7) / 8;
            var raw = new byte[(stride + 1) * height];

            using (var inflate = new DeflateStream(idat, CompressionMode.Decompress))
            {
                var total = 0;

                while (total < raw.Length)
                {
                    int n = inflate.Read(raw, total, raw.Length - total);
                    if (n <= 0) break;
                    total += n;
                }

                if (total < raw.Length) return null;
            }

            // Unfilter scanlines; for palette depths the filter distance is one byte.
            var lines = new byte[height][];

            for (var y = 0; y < height; y++)
            {
                int off = y * (stride + 1);
                byte filter = raw[off];
                var line = new byte[stride];
                Array.Copy(raw, off + 1, line, 0, stride);
                byte[] prev = y > 0 ? lines[y - 1] : null;

                for (var x = 0; x < stride; x++)
                {
                    int a = x > 0 ? line[x - 1] : 0;
                    int b = prev?[x] ?? 0;
                    int c = x > 0 && prev != null ? prev[x - 1] : 0;

                    line[x] = filter switch
                    {
                        1 => (byte)(line[x] + a),
                        2 => (byte)(line[x] + b),
                        3 => (byte)(line[x] + ((a + b) >> 1)),
                        4 => (byte)(line[x] + Paeth(a, b, c)),
                        _ => line[x],
                    };
                }

                lines[y] = line;
            }

            var pixels = new Color32[width * height];
            int mask = (1 << depth) - 1;
            int perByte = 8 / depth;

            for (var y = 0; y < height; y++)
            {
                byte[] line = lines[y];

                // PNG rows are top-down, Unity texture rows bottom-up.
                int row = (height - 1 - y) * width;

                for (var x = 0; x < width; x++)
                {
                    int idx = (line[x / perByte] >> ((perByte - 1 - (x % perByte)) * depth)) & mask;
                    int p = idx * 3;

                    if (p + 2 >= palette.Length) return null;

                    byte al = alpha != null && idx < alpha.Length ? alpha[idx] : (byte)255;
                    pixels[row + x] = new Color32(palette[p], palette[p + 1], palette[p + 2], al);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int ReadBE(byte[] d, int o) =>
            (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
    }
}
