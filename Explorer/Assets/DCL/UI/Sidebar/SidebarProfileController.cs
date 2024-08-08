using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Chat;
using DCL.ExplorePanel;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using JetBrains.Annotations;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarProfileController : ControllerBase<ProfileMenuView>
    {
        private readonly ProfileSectionController profileSectionController;
        private readonly SystemMenuController systemSectionController;

        private CancellationTokenSource profileWidgetCts = new ();

        public SidebarProfileController(
            [NotNull] ViewFactoryMethod viewFactory,
            ProfileSectionElement profileSectionElement,
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
            profileSectionController = new ProfileSectionController(profileSectionElement, identityCache, profileRepository, webRequestController, chatEntryConfiguration);
            systemSectionController = new SystemMenuController(() => viewInstance.SystemMenuView, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, identityCache, mvcManager);
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Persistent;

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) => UniTask.Never(ct);
        
        protected override void OnViewShow()
        {
            base.OnViewShow();
            profileWidgetCts = profileWidgetCts.SafeRestart();
            LaunchChildViewsAsync().Forget();
        }

        private async UniTaskVoid LaunchChildViewsAsync()
        {
            profileSectionController.LoadElements();
            await systemSectionController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token);
            await HideViewAsync(profileWidgetCts.Token);
        }
    }
}
