using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
{
    public class ProfileSectionController : ControllerBase<ProfileSectionView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IWebRequestController webRequestController;
        private readonly IGetTextureArgsFactory getTextureArgsFactory;

        private ImageController profileImageController;
        private UserNameElementController nameElementController;
        private UserWalletAddressElementController walletAddressElementController;
        private CancellationTokenSource cts;

        public ProfileSectionController(
            ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            ChatEntryConfigurationSO chatEntryConfiguration
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            nameElementController = new UserNameElementController(viewInstance!.UserNameElement, chatEntryConfiguration);
            walletAddressElementController = new UserWalletAddressElementController(viewInstance.UserWalletAddressElement);
            profileImageController = new ImageController(viewInstance.FaceSnapshotImage, webRequestController, getTextureArgsFactory);
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
            viewInstance!.FaceFrame.color = chatEntryConfiguration.GetNameColor(profile.Name);

            profileImageController!.StopLoading();

            //temporarily disabled the profile image request until we have the correct
            //picture deployment
            //await profileImageController!.RequestImageAsync(profile.Avatar.FaceSnapshotUrl, ct);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.Never(ct);

        public new void Dispose()
        {
            cts.SafeCancelAndDispose();
            profileImageController.StopLoading();
            nameElementController.Dispose();
            walletAddressElementController.Dispose();
        }
    }
}
