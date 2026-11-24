using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using WebP;

namespace DCL.WebRequests
{
    internal static class WebpTextureDecoder
    {
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

        private static (int width, int height, byte[] pixels) DecodePixels(byte[] data, int maxDimension, string url)
        {
            try
            {
                Texture2DExt.GetWebPDimensions(data, out int width, out int height);

                if (width > maxDimension || height > maxDimension || (long)width * height > MAX_PIXELS)
                    throw new InvalidDataException($"WebP dimensions exceed the texture limit ({width}x{height}): {url}");

                byte[] pixels = Texture2DExt.LoadRGBAFromWebP(data, ref width, ref height, false, out Error result);

                if (result != Error.Success)
                    throw new InvalidDataException($"Failed to decode WebP ({result}): {url}");

                return (width, height, pixels);
            }
            catch (Exception e) when (e.GetType() == typeof(Exception))
            {
                // The WebP binding throws System.Exception for malformed headers and bitstreams.
                throw new InvalidDataException($"Failed to decode WebP: {url}", e);
            }
        }
    }
}
