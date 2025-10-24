using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IAvatarScreenshotService
    {
        /// <summary>
        /// Captures the avatar preview into a color-correct (sRGB) thumbnail PNG, saves it to disk,
        /// and returns the PNG bytes for immediate UI display.
        /// </summary>
        /// <remarks>
        /// Pipeline overview:
        /// 1) Ensure main thread and stabilize the preview (hide platform, reset pose/zoom, wait a couple frames).
        /// 2) Create a temporary downscaled RenderTexture with <c>sRGB = true</c> and a UNorm8 graphics format
        ///    (BGRA8 preferred on macOS). This makes the GPU perform linear→sRGB conversion on store.
        /// 3) <see cref="Graphics.Blit(UnityEngine.Texture, UnityEngine.RenderTexture)"/> downscales and bakes the sRGB curve in one pass.
        /// 4) <see cref="UnityEngine.Rendering.AsyncGPUReadback"/> reads pixels back asynchronously (no render-thread stall).
        /// 5) Encode PNG directly from the readback buffer (<see cref="ImageConversion.EncodeNativeArrayToPNG"/>), save to the per-slot path,
        ///    and return the PNG bytes for immediate thumbnail display.
        /// 
        /// Why sRGB?: The preview renders in Linear color space (good for lighting). PNG viewers assume sRGB. Without converting,
        /// screenshots appear darker. Using an sRGB RT guarantees correct brightness on all platforms.
        /// 
        /// Threading: Unity/Graphics on main thread; file I/O on a thread pool. Temporary RT is always released in <c>finally</c>.
        /// </remarks>
        /// <param name="controller">Provides the current preview <see cref="RenderTexture"/> to capture.</param>
        /// <param name="slotIndex">Outfit slot index; used to build the destination file path.</param>
        /// <param name="ct">Cancellation token: honored before/after GPU readback and before file I/O.</param>
        /// <returns>PNG byte array on success; <c>null</c> on failure or if user ID is unavailable.</returns>
        UniTask<byte[]?> CaptureSaveAndGetPngAsync(CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct);
            
        /// <summary>
        ///     Loads a previously saved outfit screenshot from the local disk.
        /// </summary>
        /// <returns>The Texture2D if found, otherwise null.</returns>
        UniTask<Texture2D> LoadScreenshotAsync(int slotIndex, CancellationToken ct);

        /// <summary>
        ///     Deletes the screenshot file associated with a specific user and outfit slot.
        /// </summary>
        UniTask DeleteScreenshotAsync(int slotIndex, CancellationToken ct);
    }
}