using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public class AvatarScreenshotService : IAvatarScreenshotService
    {
        private readonly ISelfProfile selfProfile;
        private readonly string baseOutfitsDirectory;

        private Texture2D? screenshotTexture;
        private Material? gammaCorrectionMaterial;

        public AvatarScreenshotService(ISelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
            baseOutfitsDirectory = Path.Combine(Application.persistentDataPath, "outfits");
            if (!Directory.Exists(baseOutfitsDirectory))
                Directory.CreateDirectory(baseOutfitsDirectory);
        }

        /// <summary>
        ///     Captures the current avatar preview, creates a downscaled sRGB thumbnail,
        ///     saves the full image as a PNG file tied to the user's outfit slot,
        ///     and returns the thumbnail texture.
        ///     The method hides the platform and resets the avatar pose to stabilize the capture,
        ///     performs GPU-based downscaling and color conversion,
        ///     asynchronously reads back the image data from the GPU,
        ///     encodes it to PNG, writes it to disk on a background thread,
        ///     and finally restores the UI state and releases temporary resources.
        ///     Returns null on failure or cancellation.
        /// </summary>
        public async UniTask<Texture2D?> CaptureAndSavePngAsync(
            CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save screenshot, user ID is not available.");
                return null;
            }

            RenderTexture? downscaledSRGB = null;
            Texture2D? thumbnailTex = null;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                // Stabilize the shot: hide platform
                // reset pose/zoom, give one frame to settle
                controller.SetPlatformVisible(false);
                controller.ResetAvatarMovement();
                controller.ResetZoom();

                // NOTE: unfortunately we have to wait few frames
                // NOTE: until zoom stabilizes (if it was zoomed out thumbnail will not be
                // NOTE: taken from the reset position)
                await UniTask.DelayFrame(2, PlayerLoopTiming.PostLateUpdate, ct);
                await UniTask.Yield(PlayerLoopTiming.Update);
                
                var source = controller.CurrentRenderTexture;
                if (source == null) return null;

                int w = source.width / 2;
                int h = source.height / 2;

                var desc = new RenderTextureDescriptor(w, h)
                {
                    graphicsFormat = GetOutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits  = 0,
                    mipCount = 1, useMipMap = false
                };

                downscaledSRGB = RenderTexture.GetTemporary(desc);

                // Downscale + write into sRGB target (GPU does linear->sRGB on store)
                Graphics.Blit(source, downscaledSRGB);

                // Build thumbnail on GPU result
                thumbnailTex = MakeThumbnailFromRT(downscaledSRGB);

                // GPU readback (await without custom awaitable)
                var req = await AsyncGPUReadback
                    .Request(downscaledSRGB)
                    .WithCancellation(ct);

                if (req.hasError)
                {
                    ReportHub.LogError(ReportCategory.OUTFITS, "GPU readback failed.");
                    return null;
                }

                var srgbNative = req.GetData<byte>(); // raw sRGB bytes from RT
                using (var pngNative = ImageConversion.EncodeNativeArrayToPNG(
                           srgbNative, downscaledSRGB.graphicsFormat, (uint)w, (uint)h))
                {
                    string filePath = GetFilePathForSlot(userId, slotIndex);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    await UniTask.SwitchToThreadPool();
                    await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.Write(pngNative);
                    }
                }

                await UniTask.SwitchToMainThread();
                return thumbnailTex;
            }
            catch (OperationCanceledException)
            {
                if (thumbnailTex) Object.Destroy(thumbnailTex);
                return null;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, $"Failed to save screenshot for slot {slotIndex}: {e.Message}");
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

        public async UniTask<Texture2D> LoadScreenshotAsync(int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.Log(ReportCategory.OUTFITS, "Cannot load screenshot, user ID is not available.");
                return null;
            }

            // Get the user-specific file path.
            string filePath = GetFilePathForSlot(userId, slotIndex);

            if (!File.Exists(filePath))
                return null;

            try
            {
                await UniTask.SwitchToThreadPool();
                byte[] fileData = await File.ReadAllBytesAsync(filePath, ct);

                await UniTask.SwitchToMainThread();
                var texture = new Texture2D(2, 2);
                texture.LoadImage(fileData); // This will resize the texture automatically
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
            // 1. Get the current user ID to find the correct folder.
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.Log(ReportCategory.OUTFITS, "Cannot delete screenshot, user ID is not available.");
                return;
            }

            string filePath = GetFilePathForSlot(userId, slotIndex);

            // 2. Switch to a background thread for file I/O to prevent blocking the main thread.
            await UniTask.SwitchToThreadPool();

            try
            {
                // 3. Check if the file exists before trying to delete it to avoid exceptions.
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception e)
            {
                // Log any errors, but don't let a failed file deletion stop the rest of the process.
                ReportHub.LogError(ReportCategory.OUTFITS, $"Failed to delete screenshot file for slot {slotIndex}: {e.Message}");
            }
            finally
            {
                await UniTask.SwitchToMainThread(ct);
            }
        }

        private string GetFilePathForSlot(string userId, int slotIndex)
        {
            // Example path: .../persistentDataPath/outfits/0x123abc...def/outfit_5.png
            string userDirectory = Path.Combine(baseOutfitsDirectory, userId);
            return Path.Combine(userDirectory, $"outfit_{slotIndex}.png");
        }

        // Helper method to safely get the user ID.
        private async UniTask<string?> GetCurrentUserIdAsync(CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            return profile?.UserId;
        }

        private GraphicsFormat GetOutputGraphicsFormat()
        {
            // Prefer BGRA8 on Metal/DirectX (fast path), else RGBA8.
            var preferred = GraphicsFormat.B8G8R8A8_SRGB;
            var fallback  = GraphicsFormat.R8G8B8A8_SRGB;

            // Try preferred, then fallback; both must support RT rendering.
            if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
                return preferred;

            if (SystemInfo.IsFormatSupported(fallback, GraphicsFormatUsage.Render))
                return fallback;

            // Last resort: ask Unity for a compatible format closest to RGBA8 sRGB.
            var compat = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormatUsage.Render);
            if (compat != GraphicsFormat.None)
                return compat;

            // Extremely unlikely on modern platforms. If we’re here, sRGB RTs aren’t supported.
            // You can either:
            // 1) Return linear UNorm8 and set desc.sRGB = false, then do linear->sRGB in a Blit material,
            // 2) Or accept slightly wrong gamma in the PNG (not recommended).
            var linearFallback = GraphicsFormat.R8G8B8A8_UNorm;
            if (SystemInfo.IsFormatSupported(linearFallback, GraphicsFormatUsage.Render))
                return linearFallback;

            // Absolute last ditch: whatever Unity thinks is renderable.
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