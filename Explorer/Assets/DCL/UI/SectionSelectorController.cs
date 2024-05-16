using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
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

        void Animate(int triggerId);

        void ResetAnimator();

        RectTransform GetRectTransform();
    }

    public class SectionSelectorController<T> where T : unmanaged, Enum
    {
        private readonly Dictionary<T, ISection> sections;
        private T previousSection;

        public SectionSelectorController(Dictionary<T, ISection> sections, T initialSection)
        {
            this.sections = sections;
            previousSection = initialSection;
        }

        public void SetAnimationState(bool isOn, TabSelectorView selectorToggle)
        {
            if (selectorToggle.tabAnimator == null)
                return;

            if(isOn)
                selectorToggle.tabAnimator.SetTrigger(AnimationHashes.ACTIVE);
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
                AnimatePanelsAsync(sections[previousSection], sections[section], section, ct);
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

        public void ResetAnimators()
        {
            foreach (var keyValuePair in sections)
            {
                keyValuePair.Value.ResetAnimator();
            }
        }

        private void SetPanelsPosition(RectTransform panelClosing, RectTransform panelOpening)
        {
            panelClosing.anchoredPosition = new Vector2(panelClosing.rect.width, 0);
            panelOpening.anchoredPosition = Vector2.zero;
        }

        private void AnimatePanelsAsync(ISection panelClosing, ISection panelOpening, T newSection, CancellationToken ct)
        {
            panelOpening.Activate();
            panelOpening.ResetAnimator();
            panelOpening.Animate(AnimationHashes.IN);
            panelClosing.Animate(AnimationHashes.OUT);
            panelClosing.Deactivate();
            previousSection = newSection;
        }
    }
}
