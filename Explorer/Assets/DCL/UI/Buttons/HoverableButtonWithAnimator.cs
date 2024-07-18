using DCL.Character.CharacterMotion.Components;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.Buttons
{
    public class HoverableButtonWithAnimator : HoverableButton, IPointerEnterHandler, IPointerExitHandler
    {
        [field: Header("Animator")]
        [field: SerializeField]
        public Animator Animator { get; private set; }

        public new void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            Animator.SetTrigger(AnimationHashes.HOVER);
        }

        public new void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            Animator.SetTrigger(AnimationHashes.UNHOVER);
        }
    }
}
