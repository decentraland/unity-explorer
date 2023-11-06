using Cysharp.Threading.Tasks;
using DCL.UI;
using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class NavmapController
    {
        private NavmapView navmapView;
        private SectionSelectorController sectionSelectorController;
        private CancellationTokenSource animationCts;

        public NavmapController(NavmapView navmapView)
        {
            this.navmapView = navmapView;
            Dictionary<ExploreSections, GameObject> mapSections = new ()
            {
                { ExploreSections.Satellite, navmapView.satellite },
                { ExploreSections.StreetView, navmapView.streetView },
            };

            sectionSelectorController = new SectionSelectorController(mapSections, ExploreSections.Satellite);
            foreach (var tabSelector in navmapView.TabSelectorViews)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    (isOn) =>
                    {
                        animationCts?.Cancel();
                        animationCts?.Dispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector, animationCts.Token).Forget();
                    });
            }
            navmapView.TabSelectorViews[0].TabSelectorToggle.isOn = true;
        }

    }
}
