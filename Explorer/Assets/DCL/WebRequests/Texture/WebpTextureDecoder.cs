using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    internal static class WebpTextureDecoder
    {
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_WSA))
        private const string DECODER_LIBRARY = "libwebpdecoder";
#elif !UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS || UNITY_WEBGL)
        private const string DECODER_LIBRARY = "__Internal";
#else
        private const string DECODER_LIBRARY = "webpdecoder";
#endif

        // Limit the expanded RGBA buffer for a remote image to 64 MiB.
        private const int MAX_PIXELS = 4096 * 4096;
        private static readonly byte[] RIFF = { (byte)'R', (byte)'I', (byte)'F', (byte)'F' };
        private static readonly byte[] WEBP = { (byte)'W', (byte)'E', (byte)'B', (byte)'P' };

        internal static bool HasSignature(byte[] data) =>
            data.Length >= 12 && data.AsSpan(0, 4).SequenceEqual(RIFF) && data.AsSpan(8, 4).SequenceEqual(WEBP);

        internal static async UniTask<Texture2D> DecodeAsync(byte[] data, bool linear, string url, CancellationToken ct)
        {
            int maxDimension = SystemInfo.maxTextureSize;
            var (width, height, pixels) = await DCLTask.RunOnThreadPool(() => DecodePixels(data, maxDimension, url), cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear);

            try
            {
                texture.LoadRawTextureData(pixels);
                texture.Apply(updateMipmaps: false);
                return texture;
            }
            catch
            {
                UnityObjectUtils.SafeDestroy(texture);
                throw;
            }
        }

        private static unsafe (int width, int height, byte[] pixels) DecodePixels(byte[] data, int maxDimension, string url)
        {
            fixed (byte* input = data)
            {
                var inputSize = (UIntPtr)data.Length;

                if (WebPGetInfo(input, inputSize, out int width, out int height) == 0)
                    throw new InvalidDataException($"Invalid WebP header: {url}");

                if (width > maxDimension || height > maxDimension || (long)width * height > MAX_PIXELS)
                    throw new InvalidDataException($"WebP dimensions exceed the texture limit ({width}x{height}): {url}");

                int rowBytes = width * 4;
                byte[] pixels = new byte[rowBytes * height];

                fixed (byte* output = pixels)
                {
                    // Unity textures store the bottom row first. Decode directly into that layout.
                    byte* topRow = output + (height - 1) * rowBytes;

                    if (WebPDecodeRGBAInto(input, inputSize, topRow, (UIntPtr)pixels.Length, -rowBytes) == null)
                        throw new InvalidDataException($"Invalid WebP pixels: {url}");
                }

                return (width, height, pixels);
            }
        }

        // The decoder-only library has no dependency on the encoder's libsharpyuv.
        [DllImport(DECODER_LIBRARY, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern unsafe int WebPGetInfo(byte* data, UIntPtr size, out int width, out int height);

        [DllImport(DECODER_LIBRARY, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern unsafe byte* WebPDecodeRGBAInto(byte* data, UIntPtr size, byte* output, UIntPtr outputSize, int stride);
    }
}
