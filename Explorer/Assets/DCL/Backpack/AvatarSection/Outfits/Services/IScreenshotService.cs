using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Backpack.AvatarSection.Outfits.Services
{
    public interface IScreenshotService
    {
        UniTask<string> TakeAndUploadScreenshotAsync(CancellationToken ct);
    }
}