using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public interface ISection
    {
        void Activate();

        void Deactivate();

        RectTransform GetRectTransform();
    }

    public class SectionSelectorController
    {
        private readonly Dictionary<ExploreSections, ISection> sections;
        private ExploreSections previousSection;

        public SectionSelectorController(Dictionary<ExploreSections, ISection> sections, ExploreSections initialSection)
        {
            this.sections = sections;
            previousSection = initialSection;
        }

        public async UniTaskVoid OnTabSelectorToggleValueChangedAsync(bool isOn, TabSelectorView selectorToggle, CancellationToken ct, bool animate = true)
        {
            selectorToggle.SelectedImage.gameObject.SetActive(isOn);
            selectorToggle.UnselectedImage.gameObject.SetActive(!isOn);
            selectorToggle.SelectedText.SetActive(isOn);
            selectorToggle.UnselectedText.SetActive(!isOn);
            selectorToggle.SelectedBackground.gameObject.SetActive(isOn);

            if (!isOn || selectorToggle.section == previousSection) return;

            if (animate)
            {
                await AnimatePanelsAsync(
                    sections[previousSection],
                    sections[selectorToggle.section],
                    selectorToggle.section,
                    ct);
            }
            else
            {
                sections[previousSection].Deactivate();
                sections[selectorToggle.section].Activate();
                SetPanelsPosition(sections[previousSection].GetRectTransform(), sections[selectorToggle.section].GetRectTransform());
                previousSection = selectorToggle.section;
            }
        }

        private void SetPanelsPosition(RectTransform panelClosing, RectTransform panelOpening)
        {
            panelClosing.anchoredPosition = new Vector2(panelClosing.rect.width, 0);
            panelOpening.anchoredPosition = Vector2.zero;
        }

        private async UniTask AnimatePanelsAsync(ISection panelClosing, ISection panelOpening, ExploreSections newSection, CancellationToken ct)
        {
            panelOpening.Activate();

            RectTransform openingRectTransform = panelOpening.GetRectTransform();
            RectTransform closingRectTransform = panelClosing.GetRectTransform();

            closingRectTransform.anchoredPosition = Vector2.zero;
            openingRectTransform.anchoredPosition = new Vector2(closingRectTransform.rect.width, 0);

            await UniTask.WhenAll(
                closingRectTransform.DOAnchorPos(new Vector2(-closingRectTransform.rect.width, 0), 1f)
                                    .SetEase(Ease.OutCubic)
                                    .ToUniTask(cancellationToken: ct)
                                    .ContinueWith(() =>
                                     {
                                         //Ensures that if cancelled the closing panel is in the correct position and disabled
                                         closingRectTransform.anchoredPosition = new Vector2(-closingRectTransform.rect.width, 0);
                                         panelClosing.Deactivate();
                                     }),
                openingRectTransform.DOAnchorPos(Vector2.zero, 1f)
                                    .SetEase(Ease.OutCubic)
                                    .ToUniTask(cancellationToken: ct)
                                    .ContinueWith(() =>
                                     {
                                         //Ensures that if cancelled the panel is in the correct position
                                         closingRectTransform.anchoredPosition = Vector2.zero;
                                         previousSection = newSection;
                                     })
            );
        }
    }
}
