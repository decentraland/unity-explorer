using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public class AvatarScreenshotService : IAvatarScreenshotService
    {
        private readonly ISelfProfile selfProfile;
        private readonly string baseOutfitsDirectory = Path.Combine(Application.persistentDataPath, "outfits");

        public AvatarScreenshotService(ISelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
            if (!Directory.Exists(baseOutfitsDirectory))
                Directory.CreateDirectory(baseOutfitsDirectory);
        }

        public async UniTask<Texture2D> TakeAndSaveScreenshotAsync(CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct)
        {
            string? userId = await GetCurrentUserIdAsync(ct);
            if (string.IsNullOrEmpty(userId))
            {
                ReportHub.LogError(ReportCategory.OUTFITS, "Cannot save screenshot, user ID is not available.");
                return null;
            }

            try
            {
                await UniTask.SwitchToMainThread(ct);

                controller.SetPlatformVisible(false);
                controller.ResetAvatarMovement();
                controller.ResetZoom();
                await UniTask.DelayFrame(2, PlayerLoopTiming.LastPostLateUpdate, ct);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);

                var source = controller.CurrentRenderTexture;
                if (source == null) return null;

                ReportHub.Log(ReportCategory.OUTFITS, $"[Screenshot] Source RenderTexture sRGB: {source.sRGB}");
                ReportHub.Log(ReportCategory.OUTFITS, $"[Screenshot] Source RenderTexture format: {source.graphicsFormat}");
                int targetWidth = source.width / 2;
                int targetHeight = source.height / 2;

                // Get a temporary, smaller RenderTexture. This is highly optimized.
                var tempRT = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, source.format);

                // Use Graphics.Blit to perform a GPU-accelerated copy and scale operation.
                // This is the fastest way to downscale a texture.
                Graphics.Blit(source, tempRT);

                // Create the final Texture2D with the correct small dimensions.
                var screenshotTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

                // Set the temporary RT as active and read its (now small) contents.
                RenderTexture.active = tempRT;
                screenshotTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);

                var pixels = screenshotTexture.GetPixels();
                for (int p = 0; p < pixels.Length; p++)
                {
                    pixels[p] = pixels[p].gamma;
                }

                screenshotTexture.SetPixels(pixels);
                
                
                screenshotTexture.Apply();

                // Clean up: release the temporary RT and reset the active one.
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(tempRT);

                ct.ThrowIfCancellationRequested();

                byte[] pngBytes = screenshotTexture.EncodeToPNG();
                string filePath = GetFilePathForSlot(userId, slotIndex);

                if (filePath != null)
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                else return null;

                await File.WriteAllBytesAsync(filePath, pngBytes, ct);

                return screenshotTexture;
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, $"Failed to save screenshot for slot {slotIndex}: {e.Message}");
                return null;
            }
            finally
            {
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
    }
}