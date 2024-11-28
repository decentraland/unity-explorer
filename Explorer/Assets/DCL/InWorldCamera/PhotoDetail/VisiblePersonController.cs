using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI;
using DCL.WebRequests;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class VisiblePersonController : IDisposable
    {
        internal readonly VisiblePersonView view;
        private readonly ImageController imageController;
        private readonly IProfileRepository profileRepository;
        private readonly IMVCManager mvcManager;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly List<EquippedWearableController> wearableControllers = new();
        private readonly PhotoDetailPoolManager photoDetailPoolManager;

        private VisiblePerson? visiblePerson;
        private bool isShowingWearables;
        private bool wearablesLoaded;
        private CancellationTokenSource loadWearablesCts = new();

        public VisiblePersonController(VisiblePersonView view,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            PhotoDetailPoolManager photoDetailPoolManager)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.photoDetailPoolManager = photoDetailPoolManager;

            this.imageController = new ImageController(view.profileImage, webRequestController);
            this.view.userProfileButton.onClick.AddListener(ShowPersonPassportClicked);
            this.view.expandWearableButton.onClick.AddListener(WearableListButtonClicked);
        }

        public async UniTask Setup(VisiblePerson visiblePerson, CancellationToken ct)
        {
            this.visiblePerson = visiblePerson;
            isShowingWearables = false;
            wearablesLoaded = false;
            view.expandWearableButtonImage.localScale = Vector3.one;
            view.wearableListContainer.gameObject.SetActive(isShowingWearables);
            view.wearableListLoadingSpinner.SetActive(false);
            view.wearableListEmptyMessage.SetActive(false);
            loadWearablesCts = loadWearablesCts.SafeRestart();

            view.userName.text = visiblePerson.userName;

            Profile? profile = await profileRepository.GetAsync(visiblePerson.userAddress, ct);
            if (profile is null) return;

            await imageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public void Release()
        {
            foreach (EquippedWearableController wearableController in wearableControllers)
            {
                wearableController.Release();
                photoDetailPoolManager.ReleaseEquippedWearable(wearableController);
            }
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

            List<URN> allPersonWearables = await GetPersonUrnsAndLoadMissingWearables(visiblePerson.wearables, ct);
            UniTask[] wearableTasks = new UniTask[allPersonWearables.Count];

            for (int i = 0; i < allPersonWearables.Count; i++)
            {
                EquippedWearableController wearableController = photoDetailPoolManager.GetEquippedWearable(view.wearableListContainer);
                wearableControllers.Add(wearableController);
                if (wearableStorage.TryGetElement(allPersonWearables[i], out IWearable wearable))
                    wearableTasks[i] = wearableController.LoadWearable(wearable, ct);
            }

            ListPool<URN>.Release(allPersonWearables);

            await UniTask.WhenAll(wearableTasks);

            foreach (EquippedWearableController wearableController in wearableControllers)
                wearableController.view.gameObject.SetActive(true);

            view.wearableListLoadingSpinner.SetActive(false);
            wearablesLoaded = true;

            await UniTask.Yield(ct);

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.transform as RectTransform);
        }

        private async UniTask<List<URN>> GetPersonUrnsAndLoadMissingWearables(string[] personWearables, CancellationToken ct)
        {
            List<URN> missingUrns = ListPool<URN>.Get();
            List<URN> allUrns = ListPool<URN>.Get();
            for (int i = 0; i < personWearables.Length; i++)
            {
                URN urn = new URN(personWearables[i]).Shorten();
                allUrns.Add(urn);
                bool isPresent = wearableStorage.TryGetElement(urn, out IWearable _);

                if (!isPresent)
                    missingUrns.Add(urn);
            }

            await wearablesProvider.GetMissingDTOByURNs(missingUrns, ct);

            ListPool<URN>.Release(missingUrns);

            return allUrns;
        }

        public void Dispose()
        {
            view.userProfileButton.onClick.RemoveListener(ShowPersonPassportClicked);
            view.expandWearableButton.onClick.RemoveListener(WearableListButtonClicked);
            loadWearablesCts.SafeCancelAndDispose();
        }
    }
}
