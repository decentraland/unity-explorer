using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IAvatarScreenshotService
    {
        /// <summary>
        ///     Takes a screenshot from the provided camera/texture, saves it locally for a specific slot,
        ///     and returns the generated Texture2D.
        /// </summary>
        UniTask<Texture2D> TakeAndSaveScreenshotAsync(CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct);

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