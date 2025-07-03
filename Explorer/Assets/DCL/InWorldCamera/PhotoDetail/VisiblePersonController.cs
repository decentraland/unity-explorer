using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.Profiles;
using DG.Tweening;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using DCL.InWorldCamera.PassportBridge;
using DCL.Profiles.Helpers;
using DCL.UI.Profiles.Helpers;
using Utility;

namespace DCL.InWorldCamera.PhotoDetail
{
    /// <summary>
    ///     Handles the logic for the visible person item in the photo detail view.
    ///     It represents the user that appears in the photo and holds the user's wearables data.
    /// </summary>
    public class VisiblePersonController : IDisposable
    {
        internal readonly VisiblePersonView view;
        private readonly IProfileRepository profileRepository;
        private readonly IMVCManager mvcManager;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IPassportBridge passportBridge;
        private readonly List<EquippedWearableController> wearableControllers = new();
        private readonly PhotoDetailPoolManager photoDetailPoolManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private VisiblePerson? visiblePerson;
        private bool isShowingWearables;
        private bool wearablesLoaded;
        private CancellationTokenSource loadWearablesCts = new();

        public VisiblePersonController(VisiblePersonView view,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            IPassportBridge passportBridge,
            PhotoDetailPoolManager photoDetailPoolManager,
            ProfileRepositoryWrapper profileDataProvider)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.mvcManager = mvcManager;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.passportBridge = passportBridge;
            this.photoDetailPoolManager = photoDetailPoolManager;
            this.profileRepositoryWrapper = profileDataProvider;

            this.view.userProfileButton.onClick.AddListener(ShowPersonPassportClicked);
            this.view.expandWearableButton.onClick.AddListener(WearableListButtonClicked);
        }

        public async UniTask SetupAsync(VisiblePerson visiblePerson, CancellationToken ct)
        {
            this.visiblePerson = visiblePerson;
            isShowingWearables = false;
            wearablesLoaded = false;
            view.expandWearableButtonImage.localScale = Vector3.one;
            view.wearableListContainer.gameObject.SetActive(false);
            view.wearableListLoadingSpinner.SetActive(false);
            view.wearableListEmptyMessage.SetActive(false);
            loadWearablesCts = loadWearablesCts.SafeRestart();
            Color userColor = ProfileNameColorHelper.GetNameColor(visiblePerson.userName);

            view.userName.text = visiblePerson.userName;
            view.userName.color = userColor;
            view.userNameTag.text = $"#{visiblePerson.userAddress[^4..]}";
            view.profilePictureView.SetupOnlyColor(userColor);
            view.profilePictureView.SetLoadingState(true);

            Profile? profile = await profileRepository.GetAsync(visiblePerson.userAddress, ct);

            if (profile is not null)
            {
                view.userNameTag.gameObject.SetActive(!profile.HasClaimedName);
                await view.profilePictureView.SetupAsync(profileRepositoryWrapper, userColor, profile.Avatar.FaceSnapshotUrl, visiblePerson.userAddress, ct);
            }
            else
            {
                view.userNameTag.gameObject.SetActive(false);
                view.profilePictureView.SetDefaultThumbnail();
            }
        }

        public void Release()
        {
            foreach (EquippedWearableController wearableController in wearableControllers)
                photoDetailPoolManager.ReleaseEquippedWearable(wearableController);
            wearableControllers.Clear();
        }

        private void ShowPersonPassportClicked()
        {
            if (visiblePerson is null) return;

            passportBridge.OpenPassport(mvcManager, visiblePerson.userAddress);
        }

        private void WearableListButtonClicked()
        {
            isShowingWearables = !isShowingWearables;
            view.expandWearableButtonImage.DOScale(isShowingWearables ? new Vector3(1f, -1f, 1f) : Vector3.one, view.expandAnimationDuration);
            view.wearableListContainer.gameObject.SetActive(isShowingWearables);

            if (!wearablesLoaded)
                LoadWearablesAsync(loadWearablesCts.Token).Forget();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.transform as RectTransform);
        }

        private async UniTaskVoid LoadWearablesAsync(CancellationToken ct)
        {
            view.wearableListLoadingSpinner.SetActive(true);

            if (visiblePerson is null || visiblePerson.wearables.Length == 0)
            {
                view.wearableListEmptyMessage.SetActive(true);
                view.wearableListLoadingSpinner.SetActive(false);
                return;
            }

            List<URN> allPersonWearables = await GetPersonUrnsAndLoadMissingWearablesAsync(visiblePerson.wearables, ct);
            UniTask[] wearableTasks = new UniTask[allPersonWearables.Count];

            for (int i = 0; i < allPersonWearables.Count; i++)
            {
                EquippedWearableController wearableController = photoDetailPoolManager.GetEquippedWearable(view.wearableListContainer);
                wearableControllers.Add(wearableController);
                if (wearableStorage.TryGetElement(allPersonWearables[i], out IWearable wearable))
                    wearableTasks[i] = wearableController.LoadWearableAsync(wearable, ct);
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

        private async UniTask<List<URN>> GetPersonUrnsAndLoadMissingWearablesAsync(string[] personWearables, CancellationToken ct)
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

            await GetMissingWearablesByUrnsAsync(missingUrns, ct);

            ListPool<URN>.Release(missingUrns);

            return allUrns;
        }

        private async UniTask<IReadOnlyList<IWearable>> GetMissingWearablesByUrnsAsync(List<URN> missingUrns, CancellationToken ct)
        {
            (IReadOnlyCollection<IWearable>? maleWearables, IReadOnlyCollection<IWearable>? femaleWearables) = await UniTask.WhenAll(wearablesProvider.RequestPointersAsync(missingUrns, BodyShape.MALE, ct),
                wearablesProvider.RequestPointersAsync(missingUrns, BodyShape.FEMALE, ct));
            List<IWearable> result = new List<IWearable>();
            if (maleWearables != null)
                result.AddRange(maleWearables);

            if (femaleWearables != null)
                result.AddRange(femaleWearables);

            return result;
        }

        public void Dispose()
        {
            view.userProfileButton.onClick.RemoveListener(ShowPersonPassportClicked);
            view.expandWearableButton.onClick.RemoveListener(WearableListButtonClicked);
            loadWearablesCts.SafeCancelAndDispose();
        }
    }
}
