using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingHeaderPresenter : IDisposable
    {
        private static readonly InputMapComponent.Kind[] BLOCKED_INPUTS =
        {
            InputMapComponent.Kind.PLAYER,
            InputMapComponent.Kind.SHORTCUTS,
            InputMapComponent.Kind.CAMERA,
            InputMapComponent.Kind.IN_WORLD_CAMERA,
        };

        private const string TITLE_FORMAT = "Send a Gift to <color=#{0}>{1}</color>";
        private const int SEARCH_DEBOUNCE_MS = 500;

        public event Action<string>? OnSearchChanged;

        private readonly GiftingHeaderView view;
        private readonly IProfileRepository profileRepository;
        private readonly UserWalletAddressElementPresenter walletAddressController;
        private readonly IInputBlock inputBlock;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> profileThumbnail =
            new (ProfileThumbnailViewModel.Default());

        private CancellationTokenSource? searchCts;

        public Sprite? CurrentRecipientAvatarSprite
        {
            get;
            private set;
        }

        public GiftingHeaderPresenter(GiftingHeaderView view,
            IProfileRepository profileRepository,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.inputBlock = inputBlock;

            walletAddressController = new UserWalletAddressElementPresenter(view.UserProfileWallet);

            view.UserProfileImage.Bind(profileThumbnail);
            view.SearchBar.inputField.onSelect.AddListener(DisableInputs);
            view.SearchBar.inputField.onDeselect.AddListener(EnableInputs);
            view.SearchBar.inputField.onValueChanged.AddListener(DebounceSearch);
            view.SearchBar.clearSearchButton.onClick.AddListener(ClearSearch);
        }

        public void Clear()
        {
            if (view.SearchBar.inputField.isFocused)
                EnableInputs(string.Empty);
        }

        private void DisableInputs(string text) =>
            inputBlock.Disable(BLOCKED_INPUTS);

        private void EnableInputs(string text) =>
            inputBlock.Enable(BLOCKED_INPUTS);

        public async UniTask SetupAsync(string userId, string username, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                Profile.CompactInfo? profile = await profileRepository.GetCompactAsync(userId, ct: ct);
                if (profile == null || ct.IsCancellationRequested)
                    return;

                walletAddressController.Setup(profile.Value);
                Color userNameColor = profile.Value.UserNameColor;
                string userNameColorHex = ColorUtility.ToHtmlStringRGB(userNameColor);

                if (ct.IsCancellationRequested)
                    return;

                view.Title.text = string.Format(TITLE_FORMAT, userNameColorHex, profile.Value.Name);

                profileThumbnail.SetLoading(userNameColor);

                string faceUrl = profile.Value.FaceSnapshotUrl;

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

                if (profileThumbnail.Value.Sprite != null)
                    CurrentRecipientAvatarSprite = profileThumbnail.Value.Sprite;

                walletAddressController.Setup(profile.Value);
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
