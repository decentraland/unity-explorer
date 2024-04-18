using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    public interface ISection
    {
        void Activate();

        void Deactivate();

        RectTransform GetRectTransform();
    }

    public class SectionSelectorController<T> where T : unmanaged, Enum
    {
        private readonly Dictionary<T, ISection> sections;
        private T previousSection;
        private static readonly int ACTIVE = Animator.StringToHash("Active");

        public SectionSelectorController(Dictionary<T, ISection> sections, T initialSection)
        {
            this.sections = sections;
            previousSection = initialSection;
        }

        public void SetAnimationState(bool isOn, TabSelectorView selectorToggle)
        {
            if(isOn)
                selectorToggle.tabAnimator.SetTrigger(ACTIVE);
            else
            {
                selectorToggle.tabAnimator.Rebind();
                selectorToggle.tabAnimator.Update(0);
            }
        }

        public async UniTaskVoid OnTabSelectorToggleValueChangedAsync(bool isOn, TabSelectorView selectorToggle, T section, CancellationToken ct, bool animate = true)
        {
            if (!isOn || EnumUtils.Equals(section, previousSection))
                return;

            SetAnimationState(true, selectorToggle);

            if (animate)
            {
                await AnimatePanelsAsync(
                    sections[previousSection],
                    sections[section],
                    section,
                    ct);
            }
            else
            {
                sections[previousSection].Deactivate();
                sections[section].Activate();
                sections[previousSection].GetRectTransform().gameObject.SetActive(false);
                sections[section].GetRectTransform().gameObject.SetActive(true);
                SetPanelsPosition(sections[previousSection].GetRectTransform(), sections[section].GetRectTransform());
                previousSection = section;
            }
        }

        private void SetPanelsPosition(RectTransform panelClosing, RectTransform panelOpening)
        {
            panelClosing.anchoredPosition = new Vector2(panelClosing.rect.width, 0);
            panelOpening.anchoredPosition = Vector2.zero;
        }

        private async UniTask AnimatePanelsAsync(ISection panelClosing, ISection panelOpening, T newSection, CancellationToken ct)
        {
            panelOpening.Activate();

            RectTransform openingRectTransform = panelOpening.GetRectTransform();
            RectTransform closingRectTransform = panelClosing.GetRectTransform();
            openingRectTransform.gameObject.SetActive(true);

            closingRectTransform.anchoredPosition = Vector2.zero;
            openingRectTransform.anchoredPosition = new Vector2(closingRectTransform.rect.width, 0);

            var closingTask = closingRectTransform.DOAnchorPos(new Vector2(-closingRectTransform.rect.width, 0), 1f)
                                                  .SetEase(Ease.OutCubic)
                                                  .ToUniTask(cancellationToken: ct);

            var openingTask = openingRectTransform.DOAnchorPos(Vector2.zero, 1f)
                                                  .SetEase(Ease.OutCubic)
                                                  .ToUniTask(cancellationToken: ct);

            try { await UniTask.WhenAll(closingTask, openingTask).AttachExternalCancellation(ct); }
            catch (Exception e) when (e is not OperationCanceledException) { throw; }
            finally
            {
                //Ensures that if cancelled the closing panel is in the correct position and disabled
                closingRectTransform.anchoredPosition = new Vector2(-closingRectTransform.rect.width, 0);
                panelClosing.Deactivate();
                closingRectTransform.gameObject.SetActive(false);

                //Ensures that if cancelled the panel is in the correct position
                closingRectTransform.anchoredPosition = Vector2.zero;
                previousSection = newSection;
            }
        }
    }
}
