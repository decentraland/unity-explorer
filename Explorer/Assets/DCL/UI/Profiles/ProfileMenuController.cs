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
        private readonly ProfileSectionController profileSectionController;
        private readonly SystemMenuController systemSectionController;

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
            profileSectionController = new ProfileSectionController(() => viewInstance!.ProfileMenu, identityCache, profileRepository, profileDataProvider);
            systemSectionController = new SystemMenuController(() => viewInstance!.SystemMenuView, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, identityCache, passportBridge);
            systemSectionController.OnClosed += OnClose;
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
            profileSectionController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileMenuCts.Token).Forget();
            systemSectionController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileMenuCts.Token).Forget();
        }

        private void OnClose()
        {
            profileMenuCts.Cancel();
        }

        public override void Dispose()
        {
            base.Dispose();
            profileMenuCts.SafeCancelAndDispose();
            profileSectionController.Dispose();
            systemSectionController.Dispose();
        }
    }
}
