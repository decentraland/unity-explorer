using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Navmap;
using DCL.Settings;
using DCL.UI;
using MVC;
using System;
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
        private readonly BackpackController backpackController;
        private SectionSelectorController<ExploreSections> sectionSelectorController;
        private CancellationTokenSource animationCts;
        private TabSelectorView previousSelector;

        private Dictionary<ExploreSections, ISection> exploreSections;

        public ExplorePanelController(
            ViewFactoryMethod viewFactory,
            NavmapController navmapController,
            SettingsController settingsController,
            BackpackController backpackController) : base(viewFactory)
        {
            this.navmapController = navmapController;
            this.settingsController = settingsController;
            this.backpackController = backpackController;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Fullscreen;

        protected override void OnViewInstantiated()
        {
            exploreSections = new ()
            {
                { ExploreSections.Navmap, navmapController },
                { ExploreSections.Settings, settingsController },
                { ExploreSections.Backpack, backpackController }
            };

            sectionSelectorController = new SectionSelectorController<ExploreSections>(exploreSections, ExploreSections.Navmap);

            foreach (var keyValuePair in exploreSections)
                keyValuePair.Value.Deactivate();

            foreach (var tabSelector in viewInstance.TabSelectorMappedViews)
            {
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, tabSelector.Section, animationCts.Token).Forget();
                    }
                );
            }
        }

        protected override void OnBeforeViewShow()
        {
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
