using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Utilities;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    /// <summary>
    ///     Profile of the current user
    /// </summary>
    public class ProfileWidgetController : ControllerBase<ProfileWidgetView>
    {
        private const string GUEST_NAME = "Guest";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileChangesBus profileChangesBus;

        private readonly ReactiveProperty<ProfileThumbnailViewModel.WithColor> thumbnail = new (ProfileThumbnailViewModel.WithColor.Default());

        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileChangesBus profileChangesBus
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileChangesBus = profileChangesBus;
        }

        public override void Dispose()
        {
            loadProfileCts.SafeCancelAndDispose();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);
            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= OnIdentityCleared;

            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += OnIdentityCleared;

            viewInstance!.ProfilePictureView.Bind(thumbnail);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void OnIdentityCleared() =>
            loadProfileCts.SafeCancelAndDispose();

        private void OnIdentityChanged()
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        private void OnProfileUpdated(Profile profile)
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        private async UniTask LoadAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null) return;

            Profile? profile = await profileRepository.GetAsync(identityCache.Identity.Address, ct);

            if (profile == null) return;

            thumbnail.UpdateValue(thumbnail.Value.SetLoading(profile.UserNameColor));

            if (viewInstance!.NameLabel != null)
                viewInstance.NameLabel.text = string.IsNullOrEmpty(profile.ValidatedName) ? GUEST_NAME : profile.ValidatedName;

            if (viewInstance.AddressLabel != null)
                if (profile.HasClaimedName == false)
                    viewInstance.AddressLabel.text = profile.WalletId;

            await GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, identityCache.Identity.Address, profile.Avatar.FaceSnapshotUrl, ct);
        }
    }
}
