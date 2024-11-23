using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonController
    {
        internal readonly VisiblePersonView view;
        private readonly ImageController imageController;
        private readonly IProfileRepository profileRepository;

        private VisiblePerson visiblePerson;

        public VisiblePersonController(VisiblePersonView view,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository)
        {
            this.view = view;
            this.profileRepository = profileRepository;

            this.imageController = new ImageController(view.profileImage, webRequestController);
        }

        public async UniTask Setup(VisiblePerson visiblePerson, CancellationToken ct)
        {
            this.visiblePerson = visiblePerson;

            view.userName.text = visiblePerson.userName;

            Profile? profile = await profileRepository.GetAsync(visiblePerson.userAddress, ct);
            if (profile is null) return;

            await imageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public void Release()
        {

        }
    }
}
