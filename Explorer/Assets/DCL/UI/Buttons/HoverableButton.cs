using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;

namespace DCL.UI.Buttons
{
    public class HoverableButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnButtonHover;
        public event Action OnButtonUnhover;

        [field: SerializeField]
        public Button Button { get; private set; }

        public void OnPointerEnter(PointerEventData eventData) =>
            OnButtonHover?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            OnButtonUnhover?.Invoke();
    }
}
