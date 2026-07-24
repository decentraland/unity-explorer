using Cysharp.Threading.Tasks;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.ExplorePanel;
using DCL.UI;
using Decentraland.Kernel.Apis;
using MVC;

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
            if (isExplorePanelOpen)
                return OpenExplorerUiResult.WasAlreadyOpen;

            OpenSectionAsync(section).Forget();
            return OpenExplorerUiResult.Opened;
        }

        private async UniTask OpenSectionAsync(ExploreSections section)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExplorePanelController.IssueCommand(new ExplorePanelParameter(section)));
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
