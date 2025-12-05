using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingHeaderPresenter : IDisposable
    {
        private const int SEARCH_DEBOUNCE_MS = 500;

        public event Action<string>? OnSearchChanged;
        
        private readonly GiftingHeaderView view;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly UserWalletAddressElementController walletAddressController;
        private readonly IInputBlock inputBlock;

        private readonly ReactiveProperty<ProfileThumbnailViewModel.WithColor> profileThumbnail =
            new(new ProfileThumbnailViewModel.WithColor());
        
        private CancellationTokenSource? searchCts;

        public Sprite? CurrentRecipientAvatarSprite
        {
            get;
            private set;
        }

        public GiftingHeaderPresenter(GiftingHeaderView view,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper  profileRepositoryWrapper,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.inputBlock = inputBlock;

            walletAddressController = new UserWalletAddressElementController(view.UserProfileWallet);

            view.UserProfileImage.Bind(profileThumbnail);
            view.SearchBar.inputField.onSelect.AddListener(OnSearchSelected);
            view.SearchBar.inputField.onDeselect.AddListener(OnSearchDeselected);
            view.SearchBar.inputField.onValueChanged.AddListener(DebounceSearch);
            view.SearchBar.clearSearchButton.onClick.AddListener(ClearSearch);
        }

        private void OnSearchSelected(string text)
        {
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS,
                InputMapComponent.Kind.IN_WORLD_CAMERA,
                InputMapComponent.Kind.PLAYER);
        }

        private void OnSearchDeselected(string text)
        {
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS,
                InputMapComponent.Kind.IN_WORLD_CAMERA,
                InputMapComponent.Kind.PLAYER);
        }

        public async UniTask SetupAsync(string userId, string username, CancellationToken ct)
        {
            var profile = await profileRepository.GetAsync(userId, 0, ct: ct);
            if (profile == null) return;
            
            var userNameColor = profile.UserNameColor;
            string? userNameColorHex = ColorUtility.ToHtmlStringRGB(userNameColor);
            view.Title.text = $"Send a Gift to <color=#{userNameColorHex}>{profile.Name}</color>";

            var faceUrl = profile.Avatar.FaceSnapshotUrl;
            var vmWithColor = profileThumbnail.Value.SetColor(userNameColor);
            profileThumbnail.UpdateValue(vmWithColor);

            var sprite = await profileRepositoryWrapper.GetProfileThumbnailAsync(faceUrl, ct);
            if (sprite != null)
            {
                var loadedViewModel = ProfileThumbnailViewModel.FromLoaded(sprite, true);
                var finalViewModel = profileThumbnail.Value.SetProfile(loadedViewModel);
                profileThumbnail.UpdateValue(finalViewModel);
                CurrentRecipientAvatarSprite = sprite;
            }

            walletAddressController.Setup(profile);
        }

        private void DebounceSearch(string newText)
        {
            searchCts = searchCts.SafeRestart();
            DebounceSearchAsync(newText, searchCts.Token).Forget();
        }

        private async UniTask DebounceSearchAsync(string newText, CancellationToken ct)
        {
            await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct);
            OnSearchChanged?.Invoke(newText);
        }

        private void ClearSearch()
        {
            view.SearchBar.inputField.SetTextWithoutNotify(string.Empty);
            OnSearchChanged?.Invoke(string.Empty);
        }


        public void Dispose()
        {
            walletAddressController.Dispose();
            searchCts.SafeCancelAndDispose();
        }

        public void ClearSearchImmediate()
        {
            view.SearchBar.inputField.SetTextWithoutNotify("");
            OnSearchChanged?.Invoke(string.Empty);
        }
    }
}