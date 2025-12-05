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
        private const string TITLE_FORMAT = "Send a Gift to <color=#{0}>{1}</color>";
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
            try
            {
                ct.ThrowIfCancellationRequested();

                var profile = await profileRepository.GetAsync(userId, 0, ct: ct);
                if (profile == null || ct.IsCancellationRequested)
                    return;

                var userNameColor = profile.UserNameColor;
                string userNameColorHex = ColorUtility.ToHtmlStringRGB(userNameColor);

                if (ct.IsCancellationRequested)
                    return;

                view.Title.text = string.Format(TITLE_FORMAT, userNameColorHex, profile.Name);

                profileThumbnail.UpdateValue(profileThumbnail.Value.SetLoading(userNameColor));

                string faceUrl = profile.Avatar.FaceSnapshotUrl;

                if (!string.IsNullOrEmpty(faceUrl))
                {
                    await GetProfileThumbnailCommand.Instance.ExecuteAsync(
                        profileThumbnail,
                        null,
                        userId,
                        faceUrl,
                        ct);
                }

                if (ct.IsCancellationRequested)
                    return;

                if (profileThumbnail.Value.Thumbnail.Sprite != null)
                    CurrentRecipientAvatarSprite = profileThumbnail.Value.Thumbnail.Sprite;

                walletAddressController.Setup(profile);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
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