using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Utility.Multithreading;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public class AvatarScreenshotService : IAvatarScreenshotService
    {
        private readonly ISelfProfile selfProfile;
        private readonly string baseOutfitsDirectory;

        public AvatarScreenshotService(ISelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
            baseOutfitsDirectory = Path.Combine(Application.persistentDataPath, "outfits");
            if (!Directory.Exists(baseOutfitsDirectory))
                Directory.CreateDirectory(baseOutfitsDirectory);
        }

        /// <summary>
        ///     Captures the current avatar preview, downscales it, encodes a PNG into memory,
        ///     and returns both the thumbnail texture and the PNG bytes. Does NOT write to disk —
        ///     callers persist via <see cref="PersistPngAsync"/> only after a successful backend save.
        ///     Restores platform visibility in the finally block so the avatar doesn't stay hidden
        ///     during the caller's subsequent work.
        /// </summary>
        public async UniTask<CapturedScreenshot?> CaptureAsync(CharacterPreviewControllerBase controller, CancellationToken ct)
        {
            RenderTexture? downscaledSRGB = null;
            Texture2D? thumbnailTex = null;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                // Stabilize the shot: hide platform, reset pose/zoom, give a few frames to settle.
                controller.SetPlatformVisible(false);
                controller.ResetAvatarMovement();
                controller.ResetZoom();

                await UniTask.DelayFrame(2, PlayerLoopTiming.PostLateUpdate, ct);
                await UniTask.Yield(PlayerLoopTiming.Update);

                var source = controller.CurrentRenderTexture;
                if (source == null) return null;

                int w = source.width / 2;
                int h = source.height / 2;

                var desc = new RenderTextureDescriptor(w, h)
                {
                    graphicsFormat = GetOutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits = 0,
                    mipCount = 1, useMipMap = false
                };

                downscaledSRGB = RenderTexture.GetTemporary(desc);

                Graphics.Blit(source, downscaledSRGB);
                thumbnailTex = MakeThumbnailFromRT(downscaledSRGB);

                var req = await AsyncGPUReadback
                    .Request(downscaledSRGB)
                    .WithCancellation(ct);

                if (req.hasError)
                {
                    ReportHub.LogError(ReportCategory.OUTFITS, "GPU readback failed.");
                    if (thumbnailTex) Object.Destroy(thumbnailTex);
                    return null;
                }

                var srgbNative = req.GetData<byte>();
                byte[] pngBytes;
                using (var pngNative = ImageConversion.EncodeNativeArrayToPNG(
                           srgbNative, downscaledSRGB.graphicsFormat, (uint)w, (uint)h))
                {
                    pngBytes = pngNative.ToArray();
                }

                return new CapturedScreenshot(thumbnailTex!, pngBytes);
            }
            catch (OperationCanceledException)
            {
                if (thumbnailTex) Object.Destroy(thumbnailTex);
                return null;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, $"Failed to capture screenshot: {e.Message}");
                if (thumbnailTex) Object.Destroy(thumbnailTex);
                return null;
            }
            finally
            {
                if (downscaledSRGB != null) RenderTexture.ReleaseTemporary(downscaledSRGB);
                await UniTask.SwitchToMainThread();
                controller.SetPlatformVisible(true);
            }
        }

        public async UniTask PersistPngAsync(int slotIndex, byte[] pngBytes, CancellationToken ct)
        {
            if (pngBytes == null || pngBytes.Length == 0) return;

            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save screenshot, user ID is not available.");
                return;
            }

            string filePath = GetFilePathForSlot(userId, slotIndex);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await DCLTask.SwitchToThreadPool();
            try
            {
                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await stream.WriteAsync(pngBytes, 0, pngBytes.Length, ct);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public async UniTask<Texture2D> LoadScreenshotAsync(int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.Log(ReportCategory.OUTFITS, "Cannot load screenshot, user ID is not available.");
                return null;
            }

            string filePath = GetFilePathForSlot(userId, slotIndex);

            if (!File.Exists(filePath))
                return null;

            try
            {
                await DCLTask.SwitchToThreadPool();
                byte[] fileData = await File.ReadAllBytesAsync(filePath, ct);

                await UniTask.SwitchToMainThread();
                var texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                return texture;
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, $"Failed to load screenshot for slot {slotIndex}: {e.Message}");
                return null;
            }
        }

        public async UniTask DeleteScreenshotAsync(int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.Log(ReportCategory.OUTFITS, "Cannot delete screenshot, user ID is not available.");
                return;
            }

            string filePath = GetFilePathForSlot(userId, slotIndex);

            await DCLTask.SwitchToThreadPool();

            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.OUTFITS, $"Failed to delete screenshot file for slot {slotIndex}: {e.Message}");
            }
            finally
            {
                await UniTask.SwitchToMainThread(ct);
            }
        }

        private string GetFilePathForSlot(string userId, int slotIndex)
        {
            string userDirectory = Path.Combine(baseOutfitsDirectory, userId);
            return Path.Combine(userDirectory, $"outfit_{slotIndex}.png");
        }

        private async UniTask<string?> GetCurrentUserIdAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            return profile?.UserId;
        }

        private GraphicsFormat GetOutputGraphicsFormat()
        {
            var preferred = GraphicsFormat.B8G8R8A8_SRGB;
            var fallback = GraphicsFormat.R8G8B8A8_SRGB;

            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
                return preferred;

            if (SystemInfo.IsFormatSupported(fallback, GraphicsFormatUsage.Render))
                return fallback;

            var compat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render);
            if (compat != GraphicsFormat.None)
                return compat;

            var linearFallback = GraphicsFormat.R8G8B8A8_UNorm;
            if (SystemInfo.IsFormatSupported(linearFallback, GraphicsFormatUsage.Render))
                return linearFallback;

            return SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormatUsage.Render);
        }

        private Texture2D MakeThumbnailFromRT(RenderTexture src)
        {
            var tex = new Texture2D(src.width, src.height, src.graphicsFormat, TextureCreationFlags.None);
            Graphics.CopyTexture(src, tex);
            return tex;
        }
    }
}
