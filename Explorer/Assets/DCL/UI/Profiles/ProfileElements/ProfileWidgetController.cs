using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileWidgetController : ControllerBase<ProfileWidgetView>
    {
        private const string GUEST_NAME = "Guest";

        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IProfileChangesBus profileChangesBus;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;

        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IProfileChangesBus profileChangesBus,
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
            profileChangesBus.UnsubscribeToProfileNameChange(ProfileNameChanged);

            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            profileChangesBus.SubscribeToProfileNameChange(ProfileNameChanged);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void ProfileNameChanged(Profile profile)
        {
            SetupProfileData(profile);
            viewInstance!.ProfilePictureView.Setup(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, ct);

            if (profile == null) return;

            SetupProfileData(profile);

            await viewInstance.ProfilePictureView.SetupAsync(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct);
        }

        private void SetupProfileData(Profile profile)
        {
            if (viewInstance!.NameLabel != null) viewInstance.NameLabel.text = profile.ValidatedName ?? GUEST_NAME;

            if (viewInstance.AddressLabel != null)
                if (profile.HasClaimedName == false)
                    viewInstance.AddressLabel.text = profile.WalletId;
        }
    }
}
