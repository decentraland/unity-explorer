using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.MainUI
{
    [RequireComponent(typeof(Image))]
    public class PointerDetectionArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnEnterArea;
        public event Action OnExitArea;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnEnterArea?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnExitArea?.Invoke();
        }
    }
}
