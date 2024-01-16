using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputDetector : MonoBehaviour,IPointerEnterHandler, IPointerExitHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler
    {
        public event Action<PointerEventData> OnDragStarted;
        public event Action<PointerEventData> OnDragging;
        public event Action<PointerEventData> OnDragFinished;
        public event Action<PointerEventData> OnPointerFocus;
        public event Action<PointerEventData> OnPointerUnFocus;
        public event Action<PointerEventData> OnScrollEvent;

        public void OnBeginDrag(PointerEventData eventData) =>
            OnDragStarted?.Invoke(eventData);

        public void OnDrag(PointerEventData eventData) =>
            OnDragging?.Invoke(eventData);

        public void OnEndDrag(PointerEventData eventData) =>
            OnDragFinished?.Invoke(eventData);

        public void OnPointerEnter(PointerEventData eventData) =>
            OnPointerFocus?.Invoke(eventData);

        public void OnPointerExit(PointerEventData eventData) =>
            OnPointerUnFocus?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData)
        {
            OnScrollEvent?.Invoke(eventData);
        }
    }
}
