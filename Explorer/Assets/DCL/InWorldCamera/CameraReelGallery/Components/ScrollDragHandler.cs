using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ScrollDragHandler : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDisposable
    {
        public event Action BeginDrag;
        public event Action EndDrag;

        public void OnBeginDrag(PointerEventData eventData) =>
            BeginDrag?.Invoke();

        public void OnEndDrag(PointerEventData eventData) =>
            EndDrag?.Invoke();

        public void Dispose()
        {
            BeginDrag = null;
            EndDrag = null;
        }
    }
}
