using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using DCL.Input;
using DCL.Input.Component;
using DCL.Profiles;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public enum GiftableType { Wearables, Emotes }

    public class GiftingHeaderPresenter : IDisposable
    {
        private const int SEARCH_DEBOUNCE_MS = 500;

        public event Action<string>? OnSearchChanged;
        
        private readonly GiftingHeaderView view;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly UserWalletAddressElementController walletAddressController;
        private readonly IInputBlock inputBlock;

        private CancellationTokenSource? searchCts;

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
            // Fetch the full profile of the person we are gifting to
            var profile = await profileRepository.GetAsync(userId, 0, ct: ct);
            if (profile == null) return;

            // Set the main title text
            view.Title.text = $"Send a Gift to {profile.Name}";

            // Use the existing ProfilePictureView's setup logic
            // Note: The 'Setup' method is obsolete, but we follow the existing pattern for now.
            // You will need a reference to a ProfileRepositoryWrapper if that's what your project uses.
            // Let's assume you can get one or adapt this call to the modern `Bind` method later.
            view.UserProfileImage.Setup(profileRepositoryWrapper,
                profile.UserNameColor,
                profile.Avatar.FaceSnapshotUrl);

            // Delegate the wallet address logic to its dedicated controller
            walletAddressController.Setup(profile);
        }

        private void DebounceSearch(string newText)
        {
            searchCts = searchCts.SafeRestart();
            DebounceSearchAsync(newText, searchCts.Token).Forget();
        }

        private async UniTaskVoid DebounceSearchAsync(string newText, CancellationToken ct)
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