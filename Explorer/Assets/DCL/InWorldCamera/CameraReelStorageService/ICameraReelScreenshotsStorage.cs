using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelScreenshotsStorage
    {
        public UniTask<Texture2D> GetScreenshotImageAsync(string url, CancellationToken ct = default);

        public UniTask<Texture2D> GetScreenshotThumbnailAsync(string url, CancellationToken ct = default);
    }
}
