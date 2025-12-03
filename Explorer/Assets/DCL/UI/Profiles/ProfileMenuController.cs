using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Passport;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.UI.SystemMenu;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Profiles
{
    public class ProfileMenuController : ControllerBase<ProfileMenuView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly IProfileRepository profileRepository;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IProfileCache profileCache;
        private readonly IPassportBridge passportBridge;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private ProfileSectionPresenter? profileSectionPresenter;
        private SystemSectionPresenter? systemSectionPresenter;

        private CancellationTokenSource profileMenuCts = new ();

        public ProfileMenuController(
            ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            IProfileRepository profileRepository,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            IPassportBridge passportBridge,
            ProfileRepositoryWrapper profileDataProvider
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.profileRepository = profileRepository;
            this.world = world;
            this.playerEntity = playerEntity;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.passportBridge = passportBridge;
            this.profileDataProvider = profileDataProvider;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileSectionPresenter = new ProfileSectionPresenter(viewInstance!.ProfileMenu, identityCache, profileRepository, profileDataProvider);
            systemSectionPresenter = new SystemSectionPresenter(viewInstance!.SystemMenuView, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, identityCache, passportBridge);
            systemSectionPresenter.OnClosed += OnClose;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override void OnViewShow()
        {
            base.OnViewShow();
            viewInstance!.gameObject.SetActive(true);
        }

        protected override void OnViewClose()
        {
            profileMenuCts.Cancel();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.WhenAny(UniTask.WaitUntilCanceled(profileMenuCts.Token), UniTask.WaitUntilCanceled(ct));
        }

        protected override void OnBeforeViewShow()
        {
            base.OnBeforeViewShow();
            profileMenuCts = profileMenuCts.SafeRestart();
            profileSectionPresenter!.SetupProfile(profileMenuCts.Token);
        }

        private void OnClose()
        {
            profileMenuCts.Cancel();
        }

        public override void Dispose()
        {
            base.Dispose();
            profileMenuCts.SafeCancelAndDispose();
            profileSectionPresenter?.Dispose();
            systemSectionPresenter?.Dispose();
        }
    }
}
