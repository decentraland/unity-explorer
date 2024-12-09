using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport;
using DCL.Profiles;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoController : IDisposable
    {
        private const int VISIBLE_PERSON_DEFAULT_POOL_SIZE = 20;
        private const int EQUIPPED_WEARABLE_DEFAULT_POOL_SIZE = 20;
        private const int VISIBLE_PERSON_MAX_POOL_CAPACITY = 10000;
        private const int EQUIPPED_WEARABLE_MAX_POOL_CAPACITY = 10000;

        private readonly PhotoDetailInfoView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IMVCManager mvcManager;
        private readonly PhotoDetailPoolManager photoDetailPoolManager;
        private readonly List<VisiblePersonController> visiblePersonControllers = new ();

        private Vector2Int screenshotParcel = Vector2Int.zero;
        private string reelOwnerAddress;
        private CancellationTokenSource teleportCts = new ();

        internal event Action JumpIn;

        public PhotoDetailInfoController(PhotoDetailInfoView view,
            ICameraReelStorageService cameraReelStorageService,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWebBrowser webBrowser,
            IRealmNavigator realmNavigator,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IThumbnailProvider thumbnailProvider,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;
            this.realmNavigator = realmNavigator;
            this.mvcManager = mvcManager;

            this.photoDetailPoolManager = new PhotoDetailPoolManager(view.visiblePersonViewPrefab,
                view.equippedWearablePrefab,
                view.emptyProfileImage,
                view.unusedVisiblePersonViewContainer,
                view.unusedEquippedWearableViewContainer,
                webRequestController,
                profileRepository,
                mvcManager,
                webBrowser,
                wearableStorage,
                wearablesProvider,
                decentralandUrlsSource,
                thumbnailProvider,
                rarityBackgrounds,
                rarityColors,
                categoryIcons,
                chatEntryConfiguration,
                VISIBLE_PERSON_DEFAULT_POOL_SIZE,
                VISIBLE_PERSON_MAX_POOL_CAPACITY,
                EQUIPPED_WEARABLE_DEFAULT_POOL_SIZE,
                EQUIPPED_WEARABLE_MAX_POOL_CAPACITY);

            this.view.jumpInButton.onClick.AddListener(JumpInClicked);
            this.view.ownerProfileButton.onClick.AddListener(ShowOwnerPassportClicked);
        }

        public void Dispose()
        {
            view.jumpInButton.onClick.RemoveListener(JumpInClicked);
            view.ownerProfileButton.onClick.RemoveListener(ShowOwnerPassportClicked);
            JumpIn = null;
            teleportCts.SafeCancelAndDispose();
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

            await PopulateVisiblePersonsAsync(reelData.metadata.visiblePeople, ct);
            view.loadingState.Hide();
        }

        private async UniTask PopulateVisiblePersonsAsync(VisiblePerson[] visiblePeople, CancellationToken ct)
        {
            if (visiblePeople == null || visiblePeople.Length == 0)
                return;

            UniTask[] tasks = new UniTask[visiblePeople.Length];
            for (int i = 0; i < visiblePeople.Length; i++)
            {
                VisiblePersonController visiblePersonController = photoDetailPoolManager.GetVisiblePerson(view.visiblePersonContainer);
                visiblePersonControllers.Add(visiblePersonController);
                tasks[i] = visiblePersonController.SetupAsync(visiblePeople[i], ct);
            }

            await UniTask.WhenAll(tasks);
        }

        private void JumpInClicked()
        {
            JumpIn?.Invoke();
            realmNavigator.TeleportToParcelAsync(screenshotParcel, teleportCts.Token, false).Forget();
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
    }
}
