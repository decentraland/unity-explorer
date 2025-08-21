using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileWidgetController : ControllerBase<ProfileWidgetView>
    {
        private const int MAX_PICTURE_ATTEMPTS = 10;
        private const int ATTEMPT_PICTURE_DELAY_MS = 10000;
        private const string GUEST_NAME = "Guest";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileChangesBus profileChangesBus,
            ProfileRepositoryWrapper profileDataProvider
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileChangesBus = profileChangesBus;
            this.profileRepositoryWrapper = profileDataProvider;
        }

        public override void Dispose()
        {
            loadProfileCts.SafeCancelAndDispose();
            profileChangesBus.UnsubscribeToUpdate(OnProfileUpdated);

            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            profileChangesBus.SubscribeToUpdate(OnProfileUpdated);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void OnProfileUpdated(Profile profile)
        {
            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        private async UniTask LoadAsync(CancellationToken ct, int attempts = 0)
        {
            if (identityCache.Identity == null) return;

            Profile? profile = await profileRepository.GetAsync(identityCache.Identity.Address, ct);

            if (profile == null) return;

            if (viewInstance!.NameLabel != null)
                viewInstance.NameLabel.text = string.IsNullOrEmpty(profile.ValidatedName) ? GUEST_NAME : profile.ValidatedName;

            if (viewInstance.AddressLabel != null)
                if (profile.HasClaimedName == false)
                    viewInstance.AddressLabel.text = profile.WalletId;

            try
            {
                await viewInstance!.ProfilePictureView.SetupAsync(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct,

                    // We need to get the error so we can retry
                    rethrowError: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // We need to set a delay due to the time that takes to regenerate the thumbnail at the backend
                await UniTask.Delay(ATTEMPT_PICTURE_DELAY_MS, cancellationToken: ct);

                if (attempts < MAX_PICTURE_ATTEMPTS)
                    await LoadAsync(ct, attempts + 1);
                else
                    ReportHub.LogException(e, ReportCategory.UI);
            }
        }
    }
}
