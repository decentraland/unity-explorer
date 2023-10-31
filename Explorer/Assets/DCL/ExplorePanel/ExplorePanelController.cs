using Cysharp.Threading.Tasks;
using DCL.Navmap;
using DG.Tweening;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.ExplorePanel
{
    public enum ExploreSections
    {
        Navmap,
        Settings,
    }

    public class ExplorePanelController : ControllerBase<ExplorePanelView, MVCCheetSheet.ExampleParam>
    {
        private readonly Dictionary<ExploreSections, GameObject> exploreSections = new ();

        private ExploreSections previousSection;
        public ExplorePanelController(ViewFactoryMethod viewFactory) : base(viewFactory) { }

        public override CanvasOrdering.SORTING_LAYER SortingLayer => CanvasOrdering.SORTING_LAYER.Fullscreen;

        protected override void OnViewInstantiated()
        {
            foreach (var tabSelector in viewInstance.TabSelectorViews)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener((isOn)=>OnTabSelectorToggleValueChanged(isOn, tabSelector));
            }
            exploreSections.Add(ExploreSections.Navmap, viewInstance.GetComponentInChildren<NavmapView>().transform.parent.gameObject);
            exploreSections.Add(ExploreSections.Settings, viewInstance.GetComponentInChildren<SettingsView>().transform.parent.gameObject);
        }

        protected override UniTask WaitForCloseIntent(CancellationToken ct) =>
            viewInstance.CloseButton.OnClickAsync(ct);

        private void OnTabSelectorToggleValueChanged(bool isOn, TabSelectorView selectorToggle)
        {
            selectorToggle.SelectedImage.gameObject.SetActive(isOn);
            selectorToggle.UnselectedImage.gameObject.SetActive(!isOn);
            selectorToggle.SelectedText.SetActive(isOn);
            selectorToggle.UnselectedText.SetActive(!isOn);
            selectorToggle.SelectedBackground.gameObject.SetActive(isOn);

            if (!isOn || selectorToggle.section == previousSection) return;

            //Add Cancellation token support
            AnimatePanels(exploreSections[previousSection].transform as RectTransform, exploreSections[selectorToggle.section].transform as RectTransform).Forget();

            previousSection = selectorToggle.section;
        }

        private async UniTaskVoid AnimatePanels(RectTransform panelClosing, RectTransform panelOpening)
        {
            panelClosing.gameObject.SetActive(true);
            panelOpening.gameObject.SetActive(true);

            panelClosing.anchoredPosition = Vector2.zero;
            panelOpening.anchoredPosition = new Vector2(1920, 0);

            await UniTask.WhenAll(
                panelClosing.DOAnchorPos(new Vector2(-1920,0), 1f).SetEase(Ease.OutCubic).ToUniTask(),
                panelOpening.DOAnchorPos(new Vector2(0,0), 1f).SetEase(Ease.OutCubic).ToUniTask()
            );

            panelClosing.gameObject.SetActive(false);
        }
    }
}
