using Cysharp.Threading.Tasks;
using DCL.Navmap;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, ExplorePanelParameter>
    {
        private SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;
        private TabSelectorView previousSelector;

        public ExplorePanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SortingLayer SortLayers => CanvasOrdering.SortingLayer.Fullscreen;

        protected override void OnViewInstantiated()
        {
            Dictionary<ExploreSections, GameObject> exploreSections = new ();
            //TODO: improve as soon as we have a serializable dictionary to avoid key and values list
            for (var i = 0; i < viewInstance.Sections.Length; i++)
                exploreSections.Add(viewInstance.Sections[i], viewInstance.SectionsObjects[i]);

            sectionSelectorController = new SectionSelectorController(exploreSections, ExploreSections.Navmap);
            foreach (var tabSelector in viewInstance.TabSelectorViews)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts?.Cancel();
                        animationCts?.Dispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector, animationCts.Token).Forget();
                    }
                );
            }
        }

        protected override void OnViewShow()
        {
            foreach (var tabSelector in viewInstance.TabSelectorViews)
                if (tabSelector.section == inputData.Section)
                    tabSelector.TabSelectorToggle.isOn = true;
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);
    }

    public readonly struct ExplorePanelParameter
    {
        public readonly ExploreSections? Section;

        public ExplorePanelParameter(ExploreSections? section)
        {
            Section = section;
        }
    }
}
