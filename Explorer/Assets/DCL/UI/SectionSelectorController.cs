using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
        public class SectionSelectorController
    {
        private ExploreSections previousSection;
        private readonly Dictionary<ExploreSections, GameObject> sections;

        public SectionSelectorController(Dictionary<ExploreSections, GameObject> sections, ExploreSections initialSection)
        {
            this.sections = sections;
            previousSection = initialSection;
        }

        public async UniTaskVoid OnTabSelectorToggleValueChanged(bool isOn, TabSelectorView selectorToggle, CancellationToken ct)
        {
            selectorToggle.SelectedImage.gameObject.SetActive(isOn);
            selectorToggle.UnselectedImage.gameObject.SetActive(!isOn);
            selectorToggle.SelectedText.SetActive(isOn);
            selectorToggle.UnselectedText.SetActive(!isOn);
            selectorToggle.SelectedBackground.gameObject.SetActive(isOn);

            if (!isOn || selectorToggle.section == previousSection) return;

            await AnimatePanels(
                sections[previousSection].transform as RectTransform,
                sections[selectorToggle.section].transform as RectTransform,
                selectorToggle.section,
                ct);
        }

        private async UniTask AnimatePanels(RectTransform panelClosing, RectTransform panelOpening, ExploreSections newSection, CancellationToken ct)
        {
            panelClosing.gameObject.SetActive(true);
            panelOpening.gameObject.SetActive(true);

            panelClosing.anchoredPosition = Vector2.zero;
            panelOpening.anchoredPosition = new Vector2(1920, 0);

            await UniTask.WhenAll(
                panelClosing.DOAnchorPos(new Vector2(-1920,0), 1f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct).ContinueWith(() =>
                {
                    //Ensures that if cancelled the closing panel is in the correct position and disabled
                    panelClosing.anchoredPosition = new Vector2(-1920, 0);
                    panelClosing.gameObject.SetActive(false);
                }),
                panelOpening.DOAnchorPos(new Vector2(0,0), 1f).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct).ContinueWith(() =>
                {
                    //Ensures that if cancelled the panel is in the correct position
                    panelOpening.anchoredPosition = Vector2.zero;
                    previousSection = newSection;
                })
            );
        }
    }
}
