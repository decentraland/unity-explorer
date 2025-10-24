using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Backpack.AvatarSection.Outfits.Slots
{
    public class HoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnHoverEntered;
        public event Action OnHoverExited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (enabled)
                OnHoverEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (enabled)
                OnHoverExited?.Invoke();
        }
    }
}