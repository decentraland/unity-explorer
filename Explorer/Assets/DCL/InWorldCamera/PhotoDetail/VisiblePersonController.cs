using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonController
    {
        internal readonly VisiblePersonView view;
        private readonly ImageController imageController;
        private readonly IProfileRepository profileRepository;
        private readonly IMVCManager mvcManager;

        private VisiblePerson visiblePerson;

        public VisiblePersonController(VisiblePersonView view,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;

            this.imageController = new ImageController(view.profileImage, webRequestController);
        }

        public async UniTask Setup(VisiblePerson visiblePerson, CancellationToken ct)
        {
            this.visiblePerson = visiblePerson;

            view.userName.text = visiblePerson.userName;
            view.userProfileButton.onClick.AddListener(ShowPersonPassportClicked);

            Profile? profile = await profileRepository.GetAsync(visiblePerson.userAddress, ct);
            if (profile is null) return;

            await imageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public void Release()
        {
            view.userProfileButton.onClick.RemoveListener(ShowPersonPassportClicked);
        }

        private void ShowPersonPassportClicked()
        {
            if (visiblePerson is null) return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(visiblePerson.userAddress))).Forget();
        }
    }
}
