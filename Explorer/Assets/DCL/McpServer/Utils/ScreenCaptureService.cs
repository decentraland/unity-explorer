using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using System;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Utility;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Captures the current frame (back buffer including UI, or a world-only camera render), downscales it on
    ///     the GPU and encodes it off the main thread. One instance serves <c>screenshot</c> and
    ///     <c>capture_sequence</c>: it owns the persistent buffers and the busy gate, so textures never accumulate
    ///     and two captures never contend for the same frame.
    /// </summary>
    public sealed class ScreenCaptureService : IDisposable
    {
        public enum Format : byte
        {
            Jpg,
            Png,
        }

        /// <summary>An encoded frame. <see cref="RealtimeStamp" /> is the end-of-frame time the pixels belong to.</summary>
        public readonly struct Frame
        {
            public readonly byte[] Bytes;
            public readonly string MimeType;
            public readonly int Width;
            public readonly int Height;
            public readonly int ScreenWidth;
            public readonly int ScreenHeight;
            public readonly float RealtimeStamp;

            public Frame(byte[] bytes, string mimeType, int width, int height, int screenWidth, int screenHeight, float realtimeStamp)
            {
                Bytes = bytes;
                MimeType = mimeType;
                Width = width;
                Height = height;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
                RealtimeStamp = realtimeStamp;
            }
        }

        public const int DEFAULT_MAX_WIDTH = 1280;
        public const int MIN_WIDTH = 64;
        public const int MAX_WIDTH = 1920;
        public const int JPG_QUALITY = 75;

        private const GraphicsFormat PASSTHROUGH_GRAPHICS_FORMAT = GraphicsFormat.R8G8B8A8_UNorm;
        private const string MIME_TYPE_PNG = "image/png";
        private const string MIME_TYPE_JPEG = "image/jpeg";

        // ScreenCapture.CaptureScreenshotIntoRenderTexture fills the texture in the back buffer's native row order,
        // which on backends whose UV origin is the top (Metal, D3D) is upside down relative to a blit's.
        private static readonly bool FLIP_BACKBUFFER_CAPTURE = SystemInfo.graphicsUVStartsAtTop;
        private static readonly Vector2 FLIP_SCALE = new (1f, -1f);
        private static readonly Vector2 FLIP_OFFSET = new (0f, 1f);

        private readonly ICoroutineRunner coroutineRunner;
        private readonly World world;

        // Reused across calls by the ReadPixels fallback so repeated captures don't allocate new textures.
        private Texture2D? readPixelsBuffer;

        // The readback's data is only valid during the frame it landed, so the pixels are copied here before the
        // encode leaves the main thread. Grows to the largest capture and is reused afterwards.
        private NativeArray<byte> encodeScratch;

        // Flipped off the first time Unity refuses to encode from a worker thread; the encode then stays on the
        // main thread, still on the downscaled buffer only.
        private bool encodeOffMainThread = true;

        private string? busyOwner;

        /// <summary>Name of the tool holding the gate, or null when idle.</summary>
        public string? BusyOwner => busyOwner;

        public ScreenCaptureService(ICoroutineRunner coroutineRunner, World world)
        {
            this.coroutineRunner = coroutineRunner;
            this.world = world;
        }

        public void Dispose()
        {
            if (readPixelsBuffer != null)
                Object.Destroy(readPixelsBuffer);

            if (encodeScratch.IsCreated)
                encodeScratch.Dispose();
        }

        /// <summary>
        ///     Claims the capture gate for <paramref name="owner" />. False when another capture is running; the
        ///     caller reports <see cref="BusyOwner" /> instead of queueing, so one set of buffers suffices.
        /// </summary>
        public bool TryAcquire(string owner) =>
            Interlocked.CompareExchange(ref busyOwner, owner, null) == null;

        /// <summary>Releases the gate if <paramref name="owner" /> holds it.</summary>
        public void Release(string owner)
        {
            Interlocked.CompareExchange(ref busyOwner, null, owner);
        }

        /// <summary>
        ///     Captures one frame. Switches to the main thread to render and read back, and returns on the thread
        ///     pool with the frame already encoded, so the caller's serialization never spends main-thread time.
        /// </summary>
        public async UniTask<Frame> CaptureAsync(int maxWidth, Format format, bool worldOnly, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread(ct);

            RenderTexture? source = null;
            RenderTexture? downscaled = null;

            int width;
            int height;
            int screenWidth;
            int screenHeight;
            float realtimeStamp;
            GraphicsFormat encodeFormat;
            int pixelBytes;

            try
            {
                if (worldOnly)
                {
                    Camera camera = world.CacheCamera().GetCameraComponent(world).Camera;

                    // URP replaces the camera's internal color buffer with the target texture's descriptor
                    // (CreateRenderTextureDescriptor), so an LDR target silently downgrades the whole render
                    // to 8-bit and clamps emissives to 1.0, starving bloom and other HDR-dependent post effects.
                    // An HDR temporary keeps the pipeline HDR end-to-end; the downscale blit into the sRGB
                    // descriptor below performs the linear-to-sRGB conversion.
                    source = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.DefaultHDR);

                    // Camera.Render() is unsupported under URP: redirect the camera's output into the
                    // render texture for exactly one frame instead, then restore it.
                    RenderTexture? previousTarget = camera.targetTexture;
                    camera.targetTexture = source;

                    try { await FrameAwaiter.WaitForEndOfFrameAsync(coroutineRunner, ct); }
                    finally
                    {
                        await UniTask.SwitchToMainThread();
                        camera.targetTexture = previousTarget;
                    }

                    realtimeStamp = UnityEngine.Time.realtimeSinceStartup;
                }
                else
                {
                    // The back buffer is already display-encoded, so a Linear target stores it untouched; the
                    // passthrough descriptor below then encodes nothing a second time.
                    source = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    RenderTexture captureTarget = source;

                    // The back buffer is only complete (UI included) at the very end of the frame.
                    realtimeStamp = await FrameAwaiter.RunAtEndOfFrameAsync(coroutineRunner, () =>
                    {
                        ScreenCapture.CaptureScreenshotIntoRenderTexture(captureTarget);
                        return UnityEngine.Time.realtimeSinceStartup;
                    }, ct);
                }

                (width, height) = OutputSize(source.width, source.height, maxWidth);
                screenWidth = Screen.width;
                screenHeight = Screen.height;

                // sRGB must be assigned after graphicsFormat: its setter rewrites the format.
                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    graphicsFormat = worldOnly ? OutputGraphicsFormat() : PASSTHROUGH_GRAPHICS_FORMAT,
                    sRGB = worldOnly, msaaSamples = 1, depthBufferBits = 0,
                    mipCount = 1, useMipMap = false,
                };

                downscaled = RenderTexture.GetTemporary(descriptor);

                if (!worldOnly && FLIP_BACKBUFFER_CAPTURE)
                    Graphics.Blit(source, downscaled, FLIP_SCALE, FLIP_OFFSET);
                else
                    Graphics.Blit(source, downscaled);

                RenderTexture.ReleaseTemporary(source);
                source = null;

                encodeFormat = downscaled.graphicsFormat;

                AsyncGPUReadbackRequest readback = await AsyncGPUReadback.Request(downscaled).WithCancellation(ct);

                if (readback.hasError)
                {
                    byte[] fallbackBytes = EncodeViaReadPixels(downscaled, format);
                    await DCLTask.SwitchToThreadPool();
                    return new Frame(fallbackBytes, MimeTypeOf(format), width, height, screenWidth, screenHeight, realtimeStamp);
                }

                NativeArray<byte> rawPixels = readback.GetData<byte>();
                pixelBytes = rawPixels.Length;
                EnsureScratch(pixelBytes);
                encodeScratch.GetSubArray(0, pixelBytes).CopyFrom(rawPixels);
            }
            finally
            {
                if (source != null || downscaled != null)
                {
                    await UniTask.SwitchToMainThread();

                    if (source != null) RenderTexture.ReleaseTemporary(source);
                    if (downscaled != null) RenderTexture.ReleaseTemporary(downscaled);
                }
            }

            byte[] encoded = await EncodeAsync(encodeScratch.GetSubArray(0, pixelBytes), encodeFormat, width, height, format);
            return new Frame(encoded, MimeTypeOf(format), width, height, screenWidth, screenHeight, realtimeStamp);
        }

        /// <summary>The output size for a source downscaled to at most <paramref name="maxWidth" />, aspect ratio preserved, never upscaled.</summary>
        internal static (int width, int height) OutputSize(int sourceWidth, int sourceHeight, int maxWidth)
        {
            int width = Mathf.Min(maxWidth, sourceWidth);
            int height = Mathf.Max(1, Mathf.RoundToInt((float)sourceHeight * width / sourceWidth));
            return (width, height);
        }

        /// <summary>Encodes on the thread pool when Unity allows it, otherwise on the main thread; returns on the thread pool either way.</summary>
        private async UniTask<byte[]> EncodeAsync(NativeArray<byte> pixels, GraphicsFormat graphicsFormat, int width, int height, Format format)
        {
            if (encodeOffMainThread)
            {
                await DCLTask.SwitchToThreadPool();

                try { return Encode(pixels, graphicsFormat, width, height, format); }
                catch (UnityException e)
                {
                    encodeOffMainThread = false;
                    ReportHub.LogWarning(ReportCategory.MCP, $"Image encoding is not allowed off the main thread on this platform ({e.Message}); encoding on the main thread from now on.");
                }

                await UniTask.SwitchToMainThread();
            }

            byte[] encoded = Encode(pixels, graphicsFormat, width, height, format);
            await DCLTask.SwitchToThreadPool();
            return encoded;
        }

        private static byte[] Encode(NativeArray<byte> pixels, GraphicsFormat graphicsFormat, int width, int height, Format format)
        {
            using NativeArray<byte> encodedNative = format == Format.Png
                ? ImageConversion.EncodeNativeArrayToPNG(pixels, graphicsFormat, (uint)width, (uint)height)
                : ImageConversion.EncodeNativeArrayToJPG(pixels, graphicsFormat, (uint)width, (uint)height);

            return encodedNative.ToArray();
        }

        private void EnsureScratch(int length)
        {
            if (encodeScratch.IsCreated && encodeScratch.Length >= length)
                return;

            if (encodeScratch.IsCreated)
                encodeScratch.Dispose();

            encodeScratch = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private byte[] EncodeViaReadPixels(RenderTexture source, Format format)
        {
            if (readPixelsBuffer == null)
                readPixelsBuffer = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            else if (readPixelsBuffer.width != source.width || readPixelsBuffer.height != source.height)
                readPixelsBuffer.Reinitialize(source.width, source.height, readPixelsBuffer.graphicsFormat, false);

            RenderTexture? previousActive = RenderTexture.active;
            RenderTexture.active = source;
            readPixelsBuffer.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readPixelsBuffer.Apply();
            RenderTexture.active = previousActive;

            return format == Format.Png
                ? readPixelsBuffer.EncodeToPNG()
                : readPixelsBuffer.EncodeToJPG(JPG_QUALITY);
        }

        private static string MimeTypeOf(Format format) =>
            format == Format.Png ? MIME_TYPE_PNG : MIME_TYPE_JPEG;

        private static GraphicsFormat OutputGraphicsFormat()
        {
            var preferred = GraphicsFormat.R8G8B8A8_SRGB;

            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
                return preferred;

            return SystemInfo.GetCompatibleFormat(preferred, GraphicsFormatUsage.Render);
        }
    }
}
