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

        public new void OnEnable()
        {
            base.OnEnable();
            Button.onClick.AddListener(OnButtonPressed);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            Animator.SetTrigger(UIAnimationHashes.UNHOVER);
        }

        public new void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            Animator.SetTrigger(UIAnimationHashes.HOVER);
        }

        public new void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (!selected) Animator.SetTrigger(UIAnimationHashes.UNHOVER);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
        }

        private new void OnButtonPressed()
        {
            base.OnButtonPressed();
            selected = true;
        }
    }
}
