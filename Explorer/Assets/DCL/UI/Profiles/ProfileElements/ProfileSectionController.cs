using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileSectionController : ControllerBase<ProfileSectionView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private UserNameElementController nameElementController;
        private UserWalletAddressElementController walletAddressElementController;
        private CancellationTokenSource cts;

        public ProfileSectionController(
            ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            ProfileRepositoryWrapper profileDataProvider) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.profileRepositoryWrapper = profileDataProvider;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            nameElementController = new UserNameElementController(viewInstance!.UserNameElement);
            walletAddressElementController = new UserWalletAddressElementController(viewInstance.UserWalletAddressElement);
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            cts = cts.SafeRestart();
            SetupAsync(cts.Token).Forget();
        }

        private async UniTaskVoid SetupAsync(CancellationToken ct)
        {
            Profile? profile = await profileRepository.GetAsync(identityCache.Identity!.Address, ct);

            if (profile == null) return;

            nameElementController.Setup(profile);
            walletAddressElementController.Setup(profile);
            viewInstance!.ProfilePictureView.SetupWithDependencies(profileRepositoryWrapper, profile.UserNameColor, profile.Avatar.FaceSnapshotUrl, profile.UserId);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public new void Dispose()
        {
            cts.SafeCancelAndDispose();
            nameElementController?.Dispose();
            walletAddressElementController?.Dispose();
        }
    }
}
