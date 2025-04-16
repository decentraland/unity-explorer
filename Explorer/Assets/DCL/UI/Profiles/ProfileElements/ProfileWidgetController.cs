using Cysharp.Threading.Tasks;
using DCL.Profiles;
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
        private readonly ViewDependencies viewDependencies;

        private CancellationTokenSource? loadProfileCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        public ProfileWidgetController(ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ViewDependencies viewDependencies
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.viewDependencies = viewDependencies;
        }

        public override void Dispose()
        {
            base.Dispose();

            viewDependencies.ProfileNameChanged -= ProfileNameChanged;
        }

        protected override void OnViewInstantiated()
        {
            viewDependencies.ProfileNameChanged += ProfileNameChanged;
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();

            loadProfileCts = loadProfileCts.SafeRestart();
            LoadAsync(loadProfileCts.Token).Forget();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        private void ProfileNameChanged(Profile? profile)
        {
            if (profile == null) return;

            SetupProfileData(profile);
            viewInstance!.ProfilePictureView.SetupWithDependencies(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
        }

        private async UniTaskVoid LoadAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, ct);

            if (profile == null) return;

            SetupProfileData(profile);

            await viewInstance.ProfilePictureView.SetupWithDependenciesAsync(viewDependencies, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId, ct);
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
