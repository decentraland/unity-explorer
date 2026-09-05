using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Utilities;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    /// <summary>
    ///     Displays the Profile of the current user
    /// </summary>
    public class SidebarProfileButtonPresenter
    {
        private const string GUEST_NAME = "Guest";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ProfileWidgetView view;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());

        private CancellationTokenSource? loadProfileCts;

        public SidebarProfileButtonPresenter(
            ProfileWidgetView view,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileChangesBus profileChangesBus
        )
        {
            this.view = view;
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileChangesBus = profileChangesBus;

            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            view.ProfilePictureView.Bind(thumbnail);
        }

        public void Dispose()
        {
            loadProfileCts.SafeCancelAndDispose();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityCleared;
        }

        public void LoadProfile()
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadProfileAsync(loadProfileCts.Token).Forget();
        }

        private void OnIdentityCleared() =>
            loadProfileCts.SafeCancelAndDispose();

        private void OnIdentityChanged()
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadProfileAsync(loadProfileCts.Token).Forget();
        }

        private void OnProfileUpdated(Profile profile)
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadProfileAsync(loadProfileCts.Token).Forget();
        }

        private async UniTask LoadProfileAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null) return;

            Profile.CompactInfo? compactInfo = await profileRepository.GetCompactAsync(identityCache.Identity.Address, ct);

            if (compactInfo == null) return;
            Profile.CompactInfo profile = compactInfo.Value;
            thumbnail.SetLoading(profile.UserNameColor);

            if (view.NameLabel != null)
                view.NameLabel.text = string.IsNullOrEmpty(profile.ValidatedName) ? GUEST_NAME : profile.ValidatedName;

            if (view.AddressLabel != null)
                if (profile.HasClaimedName == false)
                    view.AddressLabel.text = profile.WalletId;

            await GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, profile, ct);
        }
    }
}
