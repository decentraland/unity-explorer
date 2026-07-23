using Cysharp.Threading.Tasks;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.ExplorePanel;
using DCL.UI;
using MVC;

namespace DCL.SceneRunner
{
    /// <summary>
    ///     DCL.Plugins-side implementation of <see cref="IExplorerUiActions" />. It lives here (rather than
    ///     next to the restricted-actions API) because it references <see cref="ExplorePanelController" />
    ///     from DCL.Social, an assembly that already depends on SceneRuntime.
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

        public OpenSectionResult OpenSection(ExploreSections section)
        {
            if (isExplorePanelOpen)
                return OpenSectionResult.AlreadyOpen;

            OpenSectionAsync(section).Forget();
            return OpenSectionResult.Opened;
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
