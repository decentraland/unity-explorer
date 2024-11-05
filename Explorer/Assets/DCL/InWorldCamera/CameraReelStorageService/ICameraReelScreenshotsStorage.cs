using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelScreenshotsStorage
    {
        public UniTask<Texture2D> GetScreenshotImageAsync(string url);

        public UniTask<Texture2D> GetScreenshotThumbnailAsync(string url);
    }
}
