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
        private readonly ProfileWidgetController profileIconWidgetController;
        private readonly ProfileWidgetController profileMenuWidgetController;
        private readonly SystemMenuController systemMenuController;

        private CancellationTokenSource profileWidgetCts = new ();
        private CancellationTokenSource systemMenuCts = new ();

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public SidebarController(ViewFactoryMethod viewFactory, IMVCManager mvcManager, ProfileWidgetController profileIconWidgetController, ProfileWidgetController profileMenuWidgetController, SystemMenuController systemMenuController)
            : base(viewFactory)
        {
            this.mvcManager = mvcManager;
            this.profileIconWidgetController = profileIconWidgetController;
            this.profileMenuWidgetController = profileMenuWidgetController;
            this.systemMenuController = systemMenuController;
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
            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(OpenProfilePopup);

            //viewInstance.emotesButton.onClick.AddListener(() => OpenExplorePanelInSection(ExploreSections.Backpack, BackpackSections.Emotes));
        }

        protected override void OnViewShow()
        {
            profileWidgetCts = profileWidgetCts.SafeRestart();
            profileIconWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token).Forget();

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(CancellationToken.None).Forget();
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            profileWidgetCts.SafeCancelAndDispose();
        }

        private void OpenProfilePopup()
        {
            viewInstance.profileMenu.gameObject.SetActive(true);
            ShowSystemMenu();
        }

        private void ShowSystemMenu()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            async UniTaskVoid ShowSystemMenuAsync(CancellationToken ct)
            {
                await systemMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0),
                    new ControllerNoData(), ct);
                await profileMenuWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0), new ControllerNoData(), profileWidgetCts.Token);
                await systemMenuController.HideViewAsync(ct);
            }

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(systemMenuCts.Token).Forget();
            else
                ShowSystemMenuAsync(systemMenuCts.Token).Forget();
        }


        private void OpenExplorePanelInSection(ExploreSections section, BackpackSections backpackSection = BackpackSections.Avatar)
        {
            mvcManager.ShowAsync(
                ExplorePanelController.IssueCommand(
                    new ExplorePanelParameter(section, backpackSection)));
        }
    }
}
