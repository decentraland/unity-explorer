using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewInputDetector : MonoBehaviour,IDragHandler, IScrollHandler
    {
        public event Action<PointerEventData> OnDraggingEvent;
        public event Action<PointerEventData> OnScrollEvent;

        public void OnDrag(PointerEventData eventData) =>
            OnDraggingEvent?.Invoke(eventData);

        public void OnScroll(PointerEventData eventData) =>
            OnScrollEvent?.Invoke(eventData);
    }
}
