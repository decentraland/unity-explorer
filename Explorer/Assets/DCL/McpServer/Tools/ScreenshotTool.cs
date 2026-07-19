using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Utility;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Captures the current frame (back buffer including UI, or a world-only camera render),
    ///     downscales it and returns it as an MCP image content block. Textures never accumulate:
    ///     temporaries are released per call and the ReadPixels fallback reuses one persistent buffer.
    /// </summary>
    public class ScreenshotTool : IMcpTool, IDisposable
    {
        private const int DEFAULT_MAX_WIDTH = 1280;
        private const int MIN_WIDTH = 64;
        private const int MAX_WIDTH = 1920;
        private const int JPG_QUALITY = 75;

        private readonly ICoroutineRunner coroutineRunner;
        private readonly World world;
        private readonly Entity playerEntity;

        // Reused across calls by the ReadPixels fallback so repeated captures don't allocate new textures.
        private Texture2D? readPixelsBuffer;

        // 1 while a capture is running; concurrent requests are rejected so one set of buffers suffices.
        private int captureGate;

        public string Name => "screenshot";

        public string Description =>
            "Capture a screenshot of what the player currently sees in the Explorer, including scene UI. "
            + "Use worldOnly to exclude all UI overlays. Returns a downscaled image plus a caption with the capture context.";

        public JObject InputSchema =>
            McpJsonSchema.Object()
                          .Integer("maxWidth", "Maximum output width in pixels (aspect ratio preserved). Default 1280.")
                          .String("quality", "Output encoding. Default jpg.", enumValues: new[] { "jpg", "png" })
                          .Boolean("worldOnly", "Render only the 3D world through the main camera, excluding UI. Default false.")
                          .Build();

        public McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public ScreenshotTool(ICoroutineRunner coroutineRunner, World world, Entity playerEntity)
        {
            this.coroutineRunner = coroutineRunner;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            if (readPixelsBuffer != null)
                Object.Destroy(readPixelsBuffer);
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int maxWidth = Mathf.Clamp(arguments.GetInt("maxWidth", DEFAULT_MAX_WIDTH), MIN_WIDTH, MAX_WIDTH);
            bool asPng = arguments.GetString("quality", "jpg") == "png";
            bool worldOnly = arguments.GetBool("worldOnly", false);

            if (Interlocked.CompareExchange(ref captureGate, 1, 0) != 0)
                return McpToolResult.Error("Another screenshot capture is already in progress; retry when it completes.");

            try { return await CaptureAsync(maxWidth, asPng, worldOnly, ct); }
            finally { Interlocked.Exchange(ref captureGate, 0); }
        }

        private async UniTask<McpToolResult> CaptureAsync(int maxWidth, bool asPng, bool worldOnly, CancellationToken ct)
        {
            Texture2D? backbufferCopy = null;
            RenderTexture? worldRender = null;
            RenderTexture? downscaled = null;

            try
            {
                int sourceWidth;
                int sourceHeight;

                if (worldOnly)
                {
                    Camera camera = world.CacheCamera().GetCameraComponent(world).Camera;

                    // URP replaces the camera's internal color buffer with the target texture's descriptor
                    // (CreateRenderTextureDescriptor), so an LDR target silently downgrades the whole render
                    // to 8-bit and clamps emissives to 1.0, starving bloom and other HDR-dependent post effects.
                    // An HDR temporary keeps the pipeline HDR end-to-end; the downscale blit into the sRGB
                    // descriptor below performs the linear-to-sRGB conversion.
                    worldRender = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.DefaultHDR);

                    // Camera.Render() is unsupported under URP: redirect the camera's output into the
                    // render texture for exactly one frame instead, then restore it.
                    RenderTexture? previousTarget = camera.targetTexture;
                    camera.targetTexture = worldRender;

                    try { await WaitForEndOfFrameAsync(ct); }
                    finally
                    {
                        await UniTask.SwitchToMainThread();
                        camera.targetTexture = previousTarget;
                    }

                    sourceWidth = worldRender.width;
                    sourceHeight = worldRender.height;
                }
                else
                {
                    backbufferCopy = await CaptureBackbufferAsync(ct);

                    if (backbufferCopy == null)
                        return McpToolResult.Error("Back buffer capture failed.");

                    sourceWidth = backbufferCopy.width;
                    sourceHeight = backbufferCopy.height;
                }

                int width = Mathf.Min(maxWidth, sourceWidth);
                int height = Mathf.Max(1, Mathf.RoundToInt((float)sourceHeight * width / sourceWidth));

                var descriptor = new RenderTextureDescriptor(width, height)
                {
                    graphicsFormat = OutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits = 0,
                    mipCount = 1, useMipMap = false,
                };

                downscaled = RenderTexture.GetTemporary(descriptor);

                if (backbufferCopy != null)
                {
                    Graphics.Blit(backbufferCopy, downscaled);

                    // The back buffer copy is a fresh Texture2D allocated by Unity on every capture: destroy it as soon as it's blitted.
                    Object.Destroy(backbufferCopy);
                    backbufferCopy = null;
                }
                else
                {
                    Graphics.Blit(worldRender, downscaled);
                    RenderTexture.ReleaseTemporary(worldRender);
                    worldRender = null;
                }

                byte[] encoded;
                string mimeType;

                AsyncGPUReadbackRequest readback = await AsyncGPUReadback.Request(downscaled).WithCancellation(ct);

                if (readback.hasError) { (encoded, mimeType) = EncodeViaReadPixels(downscaled, asPng); }
                else
                {
                    NativeArray<byte> rawPixels = readback.GetData<byte>();

                    using (NativeArray<byte> encodedNative = asPng
                               ? ImageConversion.EncodeNativeArrayToPNG(rawPixels, downscaled.graphicsFormat, (uint)width, (uint)height)
                               : ImageConversion.EncodeNativeArrayToJPG(rawPixels, downscaled.graphicsFormat, (uint)width, (uint)height, 0, JPG_QUALITY))
                        encoded = encodedNative.ToArray();

                    mimeType = asPng ? "image/png" : "image/jpeg";
                }

                Vector2Int parcel = world.Get<CharacterTransform>(playerEntity).Position.ToParcel();
                var caption = $"{width}x{height} {(worldOnly ? "world-only" : "full-view")} capture at parcel ({parcel.x},{parcel.y})";

                // Base64 conversion of the encoded image happens off the main thread.
                await DCLTask.SwitchToThreadPool();
                return McpToolResult.Image(encoded, mimeType, caption);
            }
            finally
            {
                if (backbufferCopy != null || worldRender != null || downscaled != null)
                {
                    await UniTask.SwitchToMainThread();

                    if (backbufferCopy != null) Object.Destroy(backbufferCopy);
                    if (worldRender != null) RenderTexture.ReleaseTemporary(worldRender);
                    if (downscaled != null) RenderTexture.ReleaseTemporary(downscaled);
                }
            }
        }

        private UniTask<Texture2D?> CaptureBackbufferAsync(CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource<Texture2D?>();
            coroutineRunner.StartCoroutine(CaptureBackbufferCoroutine(completion));
            return completion.Task.AttachExternalCancellation(ct);
        }

        private UniTask WaitForEndOfFrameAsync(CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource();
            coroutineRunner.StartCoroutine(SignalEndOfFrameCoroutine(completion));
            return completion.Task.AttachExternalCancellation(ct);
        }

        private static IEnumerator SignalEndOfFrameCoroutine(UniTaskCompletionSource completion)
        {
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;
            completion.TrySetResult();
        }

        private static IEnumerator CaptureBackbufferCoroutine(UniTaskCompletionSource<Texture2D?> completion)
        {
            // The back buffer is only complete (UI included) at the very end of the frame.
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;

            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();

            if (!completion.TrySetResult(texture))
                Object.Destroy(texture);
        }

        private (byte[] bytes, string mimeType) EncodeViaReadPixels(RenderTexture source, bool asPng)
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

            return asPng
                ? (readPixelsBuffer.EncodeToPNG(), "image/png")
                : (readPixelsBuffer.EncodeToJPG(JPG_QUALITY), "image/jpeg");
        }

        private static GraphicsFormat OutputGraphicsFormat()
        {
            var preferred = GraphicsFormat.R8G8B8A8_SRGB;

            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
                return preferred;

            return SystemInfo.GetCompatibleFormat(preferred, GraphicsFormatUsage.Render);
        }
    }
}
