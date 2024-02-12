using System;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputEventBus
    {
        public event Action<PointerEventData> OnDraggingEvent;
        public event Action<PointerEventData> OnScrollEvent;
        public event Action<PointerEventData> OnPointerUpEvent;
        public event Action<PointerEventData> OnPointerDownEvent;
        public event Action<AvatarWearableCategoryEnum> OnChangePreviewFocusEvent;

        public void OnDrag(PointerEventData eventData) =>
            OnDraggingEvent?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData) =>
            OnScrollEvent?.Invoke(eventData);

        public void OnChangePreviewFocus(AvatarWearableCategoryEnum category) =>
            OnChangePreviewFocusEvent?.Invoke(category);

        public void OnPointerUp(PointerEventData pointerEventData) =>
            OnPointerUpEvent?.Invoke(pointerEventData);

        public void OnPointerDown(PointerEventData pointerEventData) =>
            OnPointerDownEvent?.Invoke(pointerEventData);
    }
}
