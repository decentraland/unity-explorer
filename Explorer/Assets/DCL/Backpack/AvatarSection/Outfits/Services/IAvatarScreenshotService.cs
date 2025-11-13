using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using UnityEngine;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IAvatarScreenshotService
    {
        UniTask<Texture2D?> CaptureAndSavePngAsync(CharacterPreviewControllerBase controller, int slotIndex, CancellationToken ct);
        UniTask<Texture2D> LoadScreenshotAsync(int slotIndex, CancellationToken ct);
        UniTask DeleteScreenshotAsync(int slotIndex, CancellationToken ct);
    }
}