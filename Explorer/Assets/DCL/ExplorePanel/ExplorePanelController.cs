using Cysharp.Threading.Tasks;
using DCL.Navmap;
using DCL.UI;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.ExplorePanel
{
    public class ExplorePanelController : ControllerBase<ExplorePanelView, MVCCheetSheet.ExampleParam>
    {
        private SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;

        public ExplorePanelController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Fullscreen;

        protected override void OnViewInstantiated()
        {
            Dictionary<ExploreSections, GameObject> exploreSections = new ()
            {
                { ExploreSections.Navmap, viewInstance.GetComponentInChildren<NavmapView>().transform.parent.gameObject },
                { ExploreSections.Settings, viewInstance.GetComponentInChildren<SettingsView>().transform.parent.gameObject },
            };

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
                        sectionSelectorController.OnTabSelectorToggleValueChanged(isOn, tabSelector, animationCts.Token).Forget();
                    }
                );
            }
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);
    }
}
