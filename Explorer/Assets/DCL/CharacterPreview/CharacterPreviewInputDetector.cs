using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputDetector : MonoBehaviour,IDragHandler, IScrollHandler, IPointerUpHandler, IPointerDownHandler
    {
        public event Action<PointerEventData> OnDraggingEvent;
        public event Action<PointerEventData> OnScrollEvent;
        public event Action<PointerEventData> OnPointerUpEvent;

        public void OnPointerUp(PointerEventData eventData)
        {
            OnPointerUpEvent?.Invoke(eventData);
        }
        public void OnDrag(PointerEventData eventData) =>
            OnDraggingEvent?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData) =>
            OnScrollEvent?.Invoke(eventData);

        public void OnPointerDown(PointerEventData eventData)
        {
        }
    }
}
