using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.Profiles;
using DCL.UI.SystemMenu;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.ProfileElements
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
            IWebRequestController webRequestController,
            World world,
            Entity playerEntity,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            IMVCManager mvcManager,
            ChatEntryConfigurationSO chatEntryConfiguration
        ) : base(viewFactory)
        {
            profileSectionController = new ProfileSectionController(() => viewInstance!.ProfileMenu, identityCache, profileRepository, webRequestController, chatEntryConfiguration);
            systemSectionController = new SystemMenuController(() => viewInstance!.SystemMenuView, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, identityCache, mvcManager);
            systemSectionController.OnClosed += OnClose;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) => UniTask.Never(ct);

        protected override void OnViewInstantiated()
        {
            base.OnViewInstantiated();
            viewInstance!.CloseButton.onClick.AddListener(OnClose);
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
            CloseAsync().Forget();
        }

        private async UniTaskVoid CloseAsync()
        {
            await systemSectionController.HideViewAsync(profileMenuCts.Token);
            await profileSectionController.HideViewAsync(profileMenuCts.Token);
            await HideViewAsync(profileMenuCts.Token);
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
