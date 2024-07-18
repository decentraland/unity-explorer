using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using MVC;
using System.Threading;
using Utility;

namespace DCL.UI.Sidebar
{
    public class SidebarController : ControllerBase<SidebarView>
    {
        private readonly IMVCManager mvcManager;
        private readonly ProfileWidgetController profileWidgetController;
        private CancellationTokenSource? profileWidgetCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public SidebarController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, ProfileWidgetController profileWidgetController)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileWidgetController = profileWidgetController;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            new ();

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            viewInstance.backpackButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Backpack));
            viewInstance.settingsButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Settings));
            viewInstance.mapButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Navmap));

            //viewInstance.emotesButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Backpack, BackpackSections.Emotes));
        }

        protected override void OnViewShow()
        {
            profileWidgetCts = profileWidgetCts.SafeRestart();
            profileWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            profileWidgetCts.SafeCancelAndDispose();
        }

        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
        }
    }
}
