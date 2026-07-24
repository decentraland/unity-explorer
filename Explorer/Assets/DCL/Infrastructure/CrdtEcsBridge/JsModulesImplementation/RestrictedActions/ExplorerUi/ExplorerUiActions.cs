using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.UI;
using Decentraland.Kernel.Apis;
using MVC;
using System;

namespace DCL.Infrastructure.CrdtEcsBridge.JsModulesImplementation.RestrictedActions
{
    /// <summary>
    ///     Implementation of <see cref="IExplorerUiActions" />. The sibling asmref compiles it into
    ///     DCL.Social (not into SceneRuntime like the rest of this folder) because it references
    ///     <see cref="ExplorePanelController" />, and DCL.Social already depends on SceneRuntime.
    /// </summary>
    public class ExplorerUiActions : IExplorerUiActions
    {
        private readonly IMVCManager mvcManager;

        private bool isExplorePanelOpen;

        public ExplorerUiActions(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
            mvcManager.OnViewShowed += OnViewShowed;
            mvcManager.OnViewClosed += OnViewClosed;
        }

        public void Dispose()
        {
            mvcManager.OnViewShowed -= OnViewShowed;
            mvcManager.OnViewClosed -= OnViewClosed;
        }

        public OpenExplorerUiResult OpenSection(ExploreSections section)
        {
            // Communities availability depends on the user identity (feature flag + wallets allowlist),
            // so it cannot be gated through FeaturesRegistry like the other sections.
            if (section == ExploreSections.Communities && !CommunitiesFeatureAccess.Instance.IsUserAllowedCached())
            {
                ReportHub.Log(ReportCategory.RESTRICTED_ACTIONS, "OpenSection: the Communities feature is not available for this user");
                return OpenExplorerUiResult.RejectedFeatureDisabled;
            }

            if (isExplorePanelOpen)
                return OpenExplorerUiResult.WasAlreadyOpen;

            OpenSectionAsync(section).Forget();
            return OpenExplorerUiResult.Opened;
        }

        private async UniTask OpenSectionAsync(ExploreSections section)
        {
            try
            {
                await UniTask.SwitchToMainThread();
                await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section)));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.RESTRICTED_ACTIONS); }
        }

        private void OnViewShowed(IController controller)
        {
            if (controller is ExplorePanelController)
                isExplorePanelOpen = true;
        }

        private void OnViewClosed(IController controller)
        {
            if (controller is ExplorePanelController)
                isExplorePanelOpen = false;
        }
    }
}
