using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Backpack;
using DCL.Navmap;
using DCL.Settings;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, ExplorePanelParameter>
    {
        private readonly NavmapController navmapController;
        private readonly SettingsController settingsController;
        private readonly BackpackController backpackController;
        private readonly ProfileWidgetController profileWidgetController;
        private readonly SystemMenuController systemMenuController;

        private SectionSelectorController<ExploreSections> sectionSelectorController;
        private CancellationTokenSource animationCts;
        private CancellationTokenSource? profileWidgetCts;
        private CancellationTokenSource? systemMenuCts;
        private TabSelectorView previousSelector;

        private Dictionary<ExploreSections, ISection> exploreSections;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        public ExplorePanelController(
            ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController,
            BackpackController backpackController,
            ProfileWidgetController profileWidgetController,
            SystemMenuController systemMenuController)
            : base(viewFactory)
        {
            this.navmapController = navmapController;
            this.settingsController = settingsController;
            this.backpackController = backpackController;
            this.profileWidgetController = profileWidgetController;
            this.systemMenuController = systemMenuController;
        }

        public override void Dispose()
        {
            base.Dispose();

            profileWidgetCts.SafeCancelAndDispose();
            systemMenuCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            exploreSections = new Dictionary<ExploreSections, ISection>
            {
                { ExploreSections.Navmap, navmapController },
                { ExploreSections.Settings, settingsController },
                { ExploreSections.Backpack, backpackController },
            };


            sectionSelectorController = new SectionSelectorController<ExploreSections>(exploreSections, ExploreSections.Navmap);

            foreach (KeyValuePair<ExploreSections, ISection> keyValuePair in exploreSections)
                keyValuePair.Value.Deactivate();

            foreach (ExplorePanelTabSelectorMapping tabSelector in viewInstance.TabSelectorMappedViews)
            {
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                    isOn =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, tabSelector.Section, animationCts.Token).Forget();
                    }
                );
            }

            viewInstance.ProfileWidget.OpenProfileButton.onClick.AddListener(ShowSystemMenu);
        }

        protected override void OnBeforeViewShow()
        {
            exploreSections[inputData.Section].Activate();

            profileWidgetCts = profileWidgetCts.SafeRestart();

            profileWidgetController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0),
                                        new ControllerNoData(), profileWidgetCts.Token)
                                   .Forget();

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(CancellationToken.None).Forget();
        }

        protected override void OnViewClose()
        {
            foreach (ISection exploreSectionsValue in exploreSections.Values)
                exploreSectionsValue.Deactivate();

            profileWidgetCts.SafeCancelAndDispose();
            systemMenuCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);

        private void ShowSystemMenu()
        {
            systemMenuCts = systemMenuCts.SafeRestart();

            async UniTaskVoid ShowSystemMenuAsync(CancellationToken ct)
            {
                await systemMenuController.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0),
                    new ControllerNoData(), ct);

                await systemMenuController.HideViewAsync(ct);
            }

            if (systemMenuController.State is ControllerState.ViewFocused or ControllerState.ViewBlurred)
                systemMenuController.HideViewAsync(systemMenuCts.Token).Forget();
            else
                ShowSystemMenuAsync(systemMenuCts.Token).Forget();
        }
    }

    public readonly struct ExplorePanelParameter
    {
        public readonly ExploreSections Section;

        public ExplorePanelParameter(ExploreSections section)
        {
            Section = section;
        }
    }
}
