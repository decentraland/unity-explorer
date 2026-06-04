using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    /// <summary>
    ///     Result of an avatar screenshot capture.
    ///     <see cref="Thumbnail"/> is a <see cref="Texture2D"/> ready for UI display.
    ///     <see cref="PngBytes"/> is the same image encoded as PNG, suitable for persisting later
    ///     (only after a successful backend save).
    /// </summary>
    public readonly struct CapturedScreenshot
    {
        public readonly Texture2D Thumbnail;
        public readonly byte[] PngBytes;

        public CapturedScreenshot(Texture2D thumbnail, byte[] pngBytes)
        {
            Thumbnail = thumbnail;
            PngBytes = pngBytes;
        }
    }

    public interface IAvatarScreenshotService
    {
        /// <summary>
        ///     Captures the current avatar preview and encodes a PNG to memory.
        ///     Does NOT write to disk — call <see cref="PersistPngAsync"/> after the backend save
        ///     is confirmed.
        /// </summary>
        UniTask<CapturedScreenshot?> CaptureAsync(CharacterPreviewControllerBase controller, CancellationToken ct);

        /// <summary>
        ///     Writes a previously captured PNG to disk for the given slot.
        /// </summary>
        UniTask PersistPngAsync(int slotIndex, byte[] pngBytes, CancellationToken ct);

        UniTask<Texture2D> LoadScreenshotAsync(int slotIndex, CancellationToken ct);

        UniTask DeleteScreenshotAsync(int slotIndex, CancellationToken ct);
    }
}
