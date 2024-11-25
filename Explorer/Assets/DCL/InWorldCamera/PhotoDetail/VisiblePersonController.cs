using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonController
    {
        internal readonly VisiblePersonView view;
        private readonly ImageController imageController;
        private readonly IProfileRepository profileRepository;
        private readonly IMVCManager mvcManager;
        private readonly List<EquippedWearableController> wearableControllers = new();
        private readonly PhotoDetailPoolManager photoDetailPoolManager;

        private VisiblePerson visiblePerson;
        private bool isShowingWearables;
        private bool wearablesLoaded;
        private CancellationTokenSource loadWearablesCts = new();

        public VisiblePersonController(VisiblePersonView view,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            PhotoDetailPoolManager photoDetailPoolManager)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
            this.photoDetailPoolManager = photoDetailPoolManager;

            this.imageController = new ImageController(view.profileImage, webRequestController);
        }

        public async UniTask Setup(VisiblePerson visiblePerson, CancellationToken ct)
        {
            this.visiblePerson = visiblePerson;
            view.userProfileButton.onClick.AddListener(ShowPersonPassportClicked);
            view.expandWearableButton.onClick.AddListener(WearableListButtonClicked);

            view.userName.text = visiblePerson.userName;

            Profile? profile = await profileRepository.GetAsync(visiblePerson.userAddress, ct);
            if (profile is null) return;

            await imageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public void Release()
        {
            view.userProfileButton.onClick.RemoveListener(ShowPersonPassportClicked);
            view.expandWearableButton.onClick.RemoveListener(WearableListButtonClicked);
            isShowingWearables = false;
            wearablesLoaded = false;
            loadWearablesCts = loadWearablesCts.SafeRestart();
            wearableControllers.Clear();
        }

        private void ShowPersonPassportClicked()
        {
            if (visiblePerson is null) return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(visiblePerson.userAddress))).Forget();
        }

        private void WearableListButtonClicked()
        {
            isShowingWearables = !isShowingWearables;

            view.expandWearableButtonImage.DOScale(isShowingWearables ? new Vector3(1f, -1f, 1f) : Vector3.one, view.expandAnimationDuration);

            view.wearableListContainer.gameObject.SetActive(isShowingWearables);

            if (!wearablesLoaded)
                LoadWearables(loadWearablesCts.Token).Forget();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.transform as RectTransform);
        }

        private async UniTaskVoid LoadWearables(CancellationToken ct)
        {
            view.wearableListLoadingSpinner.SetActive(true);

            if (visiblePerson is null || visiblePerson.wearables.Length == 0)
            {
                view.wearableListEmptyMessage.SetActive(true);
                view.wearableListLoadingSpinner.SetActive(false);
                return;
            }

            UniTask[] wearableTasks = new UniTask[visiblePerson.wearables.Length];

            for (int i = 0; i < visiblePerson.wearables.Length; i++)
            {
                EquippedWearableController wearableController = photoDetailPoolManager.GetEquippedWearable(view.wearableListContainer);
                wearableControllers.Add(wearableController);
                wearableTasks[i] = wearableController.LoadWearable(visiblePerson.wearables[i], ct);
            }

            await UniTask.WhenAll(wearableTasks);

            view.wearableListLoadingSpinner.SetActive(false);
        }
    }
}
