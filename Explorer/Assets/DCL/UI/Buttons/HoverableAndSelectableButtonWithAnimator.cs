using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.Buttons
{
    public class HoverableAndSelectableButtonWithAnimator : HoverableButton, IPointerEnterHandler, IPointerExitHandler, ISelectableButton
    {
        [field: Header("Animator")]
        [field: SerializeField]
        public Animator Animator { get; private set; } = null!;

        private bool selected;

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

        public void Select()
        {
            if (!selected)
            {
                selected = true;
                Animator.ResetTrigger(UIAnimationHashes.UNHOVER);
                Animator.SetTrigger(UIAnimationHashes.HOVER);
            }
        }

        public void Deselect()
        {
            if (selected)
            {
                selected = false;
                Animator.ResetTrigger(UIAnimationHashes.HOVER);
                Animator.SetTrigger(UIAnimationHashes.UNHOVER);
            }
        }
    }
}
