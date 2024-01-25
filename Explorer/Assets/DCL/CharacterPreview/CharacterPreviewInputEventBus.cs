using System;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputEventBus
    {
        public event Action<PointerEventData> OnDraggingEvent;
        public event Action<PointerEventData> OnScrollEvent;
        public event Action<PointerEventData> OnPointerUpEvent;
        public event Action<AvatarSlotCategoryEnum> OnChangePreviewFocusEvent;

        public void OnDrag(PointerEventData eventData) =>
            OnDraggingEvent?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData) =>
            OnScrollEvent?.Invoke(eventData);

        public void OnChangeCategoryFocus(AvatarSlotCategoryEnum category) =>
            OnChangePreviewFocusEvent?.Invoke(category);

        public void OnPointerUp(PointerEventData pointerEventData)
        {
            OnPointerUpEvent?.Invoke(pointerEventData);
        }
    }

    public enum AvatarSlotCategoryEnum
    {
        Top,
        Bottom,
        Shoes,
        Head,
        Body
    }

}


