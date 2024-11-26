using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using DCL.Passport;
using DCL.Profiles;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoController : IDisposable
    {
        private const string ORIGIN = "jump in";
        private const int VISIBLE_PERSON_DEFAULT_POOL_SIZE = 20;
        private const int EQUIPPED_WEARABLE_DEFAULT_POOL_SIZE = 20;
        private const int VISIBLE_PERSON_MAX_POOL_CAPACITY = 10000;
        private const int EQUIPPED_WEARABLE_MAX_POOL_CAPACITY = 10000;

        private readonly PhotoDetailInfoView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IMVCManager mvcManager;
        private readonly PhotoDetailPoolManager photoDetailPoolManager;
        private readonly List<VisiblePersonController> visiblePersonControllers = new ();

        private Vector2Int screenshotParcel = Vector2Int.zero;
        private string reelOwnerAddress;

        internal event Action JumpIn;

        public PhotoDetailInfoController(PhotoDetailInfoView view,
            ICameraReelStorageService cameraReelStorageService,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWebBrowser webBrowser,
            IChatMessagesBus chatMessagesBus)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.chatMessagesBus = chatMessagesBus;
            this.mvcManager = mvcManager;

            this.photoDetailPoolManager = new PhotoDetailPoolManager(view.visiblePersonViewPrefab,
                view.equippedWearablePrefab,
                view.unusedVisiblePersonViewContainer,
                view.unusedEquippedWearableViewContainer,
                webRequestController,
                profileRepository,
                mvcManager,
                webBrowser,
                VISIBLE_PERSON_DEFAULT_POOL_SIZE,
                VISIBLE_PERSON_MAX_POOL_CAPACITY,
                EQUIPPED_WEARABLE_DEFAULT_POOL_SIZE,
                EQUIPPED_WEARABLE_MAX_POOL_CAPACITY);

            this.view.jumpInButton.onClick.AddListener(JumpInClicked);
            this.view.ownerProfileButton.onClick.AddListener(ShowOwnerPassportClicked);
        }

        private void ShowOwnerPassportClicked()
        {
            if (string.IsNullOrEmpty(reelOwnerAddress)) return;

            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(reelOwnerAddress))).Forget();
        }

        public async UniTask ShowPhotoDetailInfoAsync(string reelId, CancellationToken ct)
        {
            Release();
            view.loadingState.Show();
            CameraReelResponse reelData = await cameraReelStorageService.GetScreenshotsMetadataAsync(reelId, ct);

            screenshotParcel.x = Convert.ToInt32(reelData.metadata.scene.location.x);
            screenshotParcel.y = Convert.ToInt32(reelData.metadata.scene.location.y);

            reelOwnerAddress = reelData.metadata.userAddress;

            view.dateText.SetText(ReelUtility.GetDateTimeFromString(reelData.metadata.dateTime).ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture));
            view.ownerName.SetText(reelData.metadata.userName);
            view.sceneInfo.SetText($"{reelData.metadata.scene.name}, {screenshotParcel.x}, {screenshotParcel.y}");

            await PopulateVisiblePersons(reelData.metadata.visiblePeople, ct);
            view.loadingState.Hide();
        }

        private async UniTask PopulateVisiblePersons(VisiblePerson[] visiblePeople, CancellationToken ct)
        {
            if (visiblePeople == null || visiblePeople.Length == 0)
                return;

            UniTask[] tasks = new UniTask[visiblePeople.Length];
            for (int i = 0; i < visiblePeople.Length; i++)
            {
                VisiblePersonController visiblePersonController = photoDetailPoolManager.GetVisiblePerson(view.visiblePersonContainer);
                visiblePersonControllers.Add(visiblePersonController);
                tasks[i] = visiblePersonController.Setup(visiblePeople[i], ct);
            }

            await UniTask.WhenAll(tasks);
        }

        private void JumpInClicked()
        {
            JumpIn?.Invoke();
            chatMessagesBus.Send($"/{ChatCommandsUtils.COMMAND_GOTO} {screenshotParcel.x},{screenshotParcel.y}", ORIGIN);
        }

        public void Release()
        {
            for(int i = 0; i < visiblePersonControllers.Count; i++)
            {
                visiblePersonControllers[i].Release();
                photoDetailPoolManager.ReleaseVisiblePerson(visiblePersonControllers[i]);
            }
            visiblePersonControllers.Clear();
        }

        public void Dispose()
        {
            view.jumpInButton.onClick.RemoveListener(JumpInClicked);
            view.ownerProfileButton.onClick.RemoveListener(ShowOwnerPassportClicked);
            JumpIn = null;
        }
    }
}
