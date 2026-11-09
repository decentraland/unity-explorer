using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Passport;
using DCL.UI.Profiles.Helpers;
using DCL.UI.ProfileElements;
using DCL.UI.SystemMenu;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Profiles
{
    public class ProfileMenuController : ControllerBase<ProfileMenuView>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly UserLogout logout;
        private readonly IPassportBridge passportBridge;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private ProfileSectionPresenter? profileSectionPresenter;
        private SystemSectionPresenter? systemSectionPresenter;

        private CancellationTokenSource profileMenuCts = new ();

        public ProfileMenuController(
            ViewFactoryMethod viewFactory,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            UserLogout logout,
            IPassportBridge passportBridge,
            ProfileRepositoryWrapper profileDataProvider
        ) : base(viewFactory)
        {
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.logout = logout;
            this.passportBridge = passportBridge;
            this.profileDataProvider = profileDataProvider;
        }

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();

            profileSectionPresenter = new ProfileSectionPresenter(viewInstance!.ProfileMenu, identityCache, profileDataProvider);
            systemSectionPresenter = new SystemSectionPresenter(viewInstance!.SystemMenuView, logout, webBrowser, identityCache, passportBridge);
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

            if (systemSectionPresenter != null)
                systemSectionPresenter.OnClosed -= OnClose;

            profileMenuCts.SafeCancelAndDispose();
            profileSectionPresenter?.Dispose();
            systemSectionPresenter?.Dispose();
        }
    }
}
