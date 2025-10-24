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

        public async UniTask<byte[]?> CaptureSaveAndGetPngAsync(
            CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save screenshot, user ID is not available.");
                return null;
            }

            RenderTexture? downscaledSRGB = null;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                // Stabilize the shot: hide platform,
                // reset pose, give the frame pipeline a tick to settle
                controller.SetPlatformVisible(false);
                controller.ResetAvatarMovement();
                controller.ResetZoom();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                var source = controller.CurrentRenderTexture;
                if (source == null) return null;

                // Thumbnail size (halve the preview resolution)
                // adjust if you prefer a fixed cap (e.g., 512x512)
                int w = source.width  / 2;
                int h = source.height / 2;

                // Build a UNorm8 + sRGB-capable RT so the GPU performs linear->sRGB conversion on store.
                // Prefer BGRA8 on macOS/Metal; fallback to RGBA8 elsewhere.
                var desc = new RenderTextureDescriptor(w, h)
                {
                    graphicsFormat  = OutFmt(), sRGB = true, msaaSamples = 1, depthBufferBits = 0,
                    mipCount = 1, useMipMap = false
                };

                downscaledSRGB = RenderTexture.GetTemporary(desc);

                // GPU downscale + sRGB encode in one pass
                Graphics.Blit(source, downscaledSRGB);

                // Async readback: avoids render-thread stalls, completion occurs on a later frame
                var req = await AsyncGPUReadback.Request(downscaledSRGB).WithCancellation(ct);
                if (req.hasError)
                {
                    ReportHub.LogError(ReportCategory.OUTFITS, "GPU readback failed.");
                    return null;
                }

                ct.ThrowIfCancellationRequested();

                var srgbNative = req.GetData<byte>();

                byte[] pngBytes;
                // Encode PNG directly from the readback buffer
                // (no Texture2D.Get/SetPixels, no CPU gamma conversion)
                using (var pngNative = ImageConversion.EncodeNativeArrayToPNG(
                           srgbNative, downscaledSRGB.graphicsFormat, (uint)w, (uint)h))
                {
                    pngBytes = pngNative.ToArray();
                }

                // Persist to disk on a background thread
                string filePath = GetFilePathForSlot(userId, slotIndex);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                await UniTask.SwitchToThreadPool();
                await File.WriteAllBytesAsync(filePath, pngBytes, ct);
                await UniTask.SwitchToMainThread(ct);

                // Return bytes so UI can display thumbnail
                // immediately (no disk read required)
                return pngBytes;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, $"Failed to save screenshot for slot {slotIndex}: {e.Message}");
                return null;
            }
            finally
            {
                if (downscaledSRGB != null) RenderTexture.ReleaseTemporary(downscaledSRGB);
                await UniTask.SwitchToMainThread(ct);
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

        private GraphicsFormat PickUNorm8()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B8G8R8A8_UNorm, GraphicsFormatUsage.Render))
                return GraphicsFormat.B8G8R8A8_UNorm;
            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        private GraphicsFormat OutFmt()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B8G8R8A8_UNorm, GraphicsFormatUsage.Render))
                return GraphicsFormat.B8G8R8A8_UNorm;
            return GraphicsFormat.R8G8B8A8_UNorm;
        }
    }
}