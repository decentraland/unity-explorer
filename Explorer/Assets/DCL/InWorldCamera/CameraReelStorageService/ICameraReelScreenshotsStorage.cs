using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelStorageService
{
    public interface ICameraReelScreenshotsStorage
    {
        public UniTask<Texture2D> GetScreenshotImage(string url);

        public UniTask<Texture2D> GetScreenshotThumbnail(string url);
    }
}
