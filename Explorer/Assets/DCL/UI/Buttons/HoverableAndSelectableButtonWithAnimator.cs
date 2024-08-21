using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.Buttons
{
    public class HoverableAndSelectableButtonWithAnimator : HoverableButton, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [field: Header("Animator")]
        [field: SerializeField]
        public Animator Animator { get; private set; }

        private bool selected;

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        public new void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (!selected)
            {
                Animator.ResetTrigger(UIAnimationHashes.UNHOVER);
                Animator.SetTrigger(UIAnimationHashes.HOVER);
            }
        }

        public new void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (!selected)
            {
                Animator.ResetTrigger(UIAnimationHashes.HOVER);
                Animator.SetTrigger(UIAnimationHashes.UNHOVER);
            }
        }
    }
}
