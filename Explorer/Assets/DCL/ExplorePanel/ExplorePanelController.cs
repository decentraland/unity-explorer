using Cysharp.Threading.Tasks;
using DCL.Navmap;
using DCL.Settings;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, ExplorePanelParameter>
    {
        private readonly NavmapController navmapController;
        private readonly SettingsController settingsController;
        private SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;
        private TabSelectorView previousSelector;

        private Dictionary<ExploreSections, ISection> exploreSections;

        public ExplorePanelController(
            ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController) : base(viewFactory)
        {
            this.navmapController = navmapController;
            this.settingsController = settingsController;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        protected override void OnViewInstantiated()
        {
            exploreSections = new ()
            {
                { ExploreSections.Navmap, navmapController },
                { ExploreSections.Settings, settingsController },
            };

            sectionSelectorController = new SectionSelectorController(exploreSections, ExploreSections.Navmap);

            foreach (var keyValuePair in exploreSections)
                keyValuePair.Value.Deactivate();

            foreach (var tabSelector in viewInstance.TabSelectorViews)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector, animationCts.Token).Forget();
                    }
                );
            }
        }

        protected override void OnBeforeViewShow()
        {
            viewInstance.TabSelectorViews[0].TabSelectorToggle.isOn = true;
            exploreSections[inputData.Section].Activate();
        }

        protected override void OnViewClose()
        {
            foreach (ISection exploreSectionsValue in exploreSections.Values)
                exploreSectionsValue.Deactivate();
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);
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
