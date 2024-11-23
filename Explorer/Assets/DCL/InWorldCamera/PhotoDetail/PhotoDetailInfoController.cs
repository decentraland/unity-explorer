using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.InWorldCamera.ReelActions;
using DCL.Profiles;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailInfoController
    {
        private const int VISIBLE_PERSON_DEFAULT_POOL_SIZE = 20;
        private const int VISIBLE_PERSON_MAX_POOL_CAPACITY = 10000;

        private readonly PhotoDetailInfoView view;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly PhotoDetailPoolManager photoDetailPoolManager;
        private readonly List<VisiblePersonController> visiblePersonControllers = new ();

        public PhotoDetailInfoController(PhotoDetailInfoView view,
            ICameraReelStorageService cameraReelStorageService,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository)
        {
            this.view = view;
            this.cameraReelStorageService = cameraReelStorageService;

            this.photoDetailPoolManager = new PhotoDetailPoolManager(view.visiblePersonViewPrefab, view.unusedVisiblePersonViewContainer, webRequestController, profileRepository, VISIBLE_PERSON_DEFAULT_POOL_SIZE, VISIBLE_PERSON_MAX_POOL_CAPACITY);
        }

        public async UniTask ShowPhotoDetailInfoAsync(string reelId, CancellationToken ct)
        {
            view.loadingState.SetActive(true);
            CameraReelResponse reelData = await cameraReelStorageService.GetScreenshotsMetadataAsync(reelId, ct);

            view.dateText.SetText(ReelUtility.GetDateTimeFromString(reelData.metadata.dateTime).ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture));
            view.ownerName.SetText(reelData.metadata.userName);
            view.sceneInfo.SetText($"{reelData.metadata.scene.name}, {reelData.metadata.scene.location.x}, {reelData.metadata.scene.location.y}");

            await PopulateVisiblePersons(reelData.metadata.visiblePeople, ct);
            view.loadingState.SetActive(false);
        }

        private async UniTask PopulateVisiblePersons(VisiblePerson[] visiblePeople, CancellationToken ct)
        {
            UniTask[] tasks = new UniTask[visiblePeople.Length];
            for (int i = 0; i < visiblePeople.Length; i++)
            {
                VisiblePersonController visiblePersonController = photoDetailPoolManager.GetVisiblePerson(view.visiblePersonContainer);
                visiblePersonControllers.Add(visiblePersonController);
                tasks[i] = visiblePersonController.Setup(visiblePeople[i], ct);
            }

            await UniTask.WhenAll(tasks);
        }

        public void Release()
        {
            for(int i = 0; i < visiblePersonControllers.Count; i++)
            {
                visiblePersonControllers[i].Release();
                photoDetailPoolManager.ReleaseVisiblePerson(visiblePersonControllers[i]);
            }
        }
    }
}
