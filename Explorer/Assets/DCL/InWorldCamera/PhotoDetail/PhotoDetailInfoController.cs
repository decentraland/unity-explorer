using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoController
    {
        private readonly PhotoDetailInfoView view;
        private readonly ICameraReelStorageService cameraReelStorageService;

        public PhotoDetailInfoController(PhotoDetailInfoView view,
            ICameraReelStorageService cameraReelStorageService)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
        }

        public async UniTask ShowPhotoDetailInfoAsync(string reelId, CancellationToken ct)
        {
            // Show photo detail info
        }
    }
}
